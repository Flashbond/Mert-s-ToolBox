using Colossal.Mathematics;
using Game.Simulation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MertsToolBox.Systems.SubSystems
{
    internal sealed class TerrainLaneOverlay
    {
        private const BindingFlags k_Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const int k_SplatRefreshDelay = 8;

        private readonly TerrainSystem m_Terrain;
        private readonly FieldInfo m_LaneListField;
        private readonly FieldInfo m_CascadeCullingField;
        private readonly FieldInfo m_UpdateAreaField;
        private readonly List<FieldInfo> m_JobFields = new();
        private readonly List<FieldInfo> m_CascadeJobFields = new();

        private readonly List<TerrainSystem.LaneSection> m_Lanes = new();
        private float4 m_Area;
        private int m_Base = -1;
        private int m_Count;
        private TerrainSystem.LaneSection m_First;
        private float4 m_RefreshArea;
        private int m_RefreshCountdown;

        public bool Ok { get; }
        public bool RefreshPending => m_RefreshCountdown > 0;
        public bool Active => m_Lanes.Count > 0;
        public int FramesSinceInject { get; private set; }
        public float4 Area => m_Area;

        /// <summary>
        /// Resolves the TerrainSystem lane list and every job handle that reads it via reflection.
        /// The per-cascade culling jobs (CullRoadsCascadeJob) iterate the lane list with a length read when they START,
        /// but their output list was sized from the length read when they were SCHEDULED. If the overlay grows the lane
        /// list in between, the job writes past the end of its output buffer (native heap corruption, later crash).
        /// So the overlay may only touch the list after completing those jobs; if their handles cannot be found, the
        /// overlay stays disabled (Ok = false) instead of guessing.
        /// </summary>
        public TerrainLaneOverlay(TerrainSystem terrain)
        {
            m_Terrain = terrain;
            Type ts = typeof(TerrainSystem);
            m_LaneListField = ts.GetField("m_LaneCullList", k_Flags);
            // TerrainSystem.OnAreaChanged does two things: it queues a cascade redraw (m_UpdateArea) AND tells
            // GroundHeightSystem that the ground changed there, which re-snaps every object and net in the area -
            // including the tool's own preview roads, which the game then rebuilds (flicker, lost "will be removed"
            // outlines). The overlay is only a visual preview, so it asks for the redraw alone.
            m_UpdateAreaField = ts.GetField("m_UpdateArea", k_Flags);
            if (m_UpdateAreaField != null && m_UpdateAreaField.FieldType != typeof(float4))
                m_UpdateAreaField = null;
            foreach (FieldInfo f in ts.GetFields(k_Flags))
            {
                if (f.FieldType == typeof(JobHandle))
                    m_JobFields.Add(f);
            }

            m_CascadeCullingField = FindCascadeCulling(ts, m_CascadeJobFields);

            bool laneList = m_LaneListField != null && m_LaneListField.FieldType == typeof(NativeList<TerrainSystem.LaneSection>);
            Ok = laneList && m_CascadeCullingField != null && m_CascadeJobFields.Count > 0;
        }

        /// <summary>
        /// Finds the TerrainSystem field holding the per-cascade culling state: an array or list whose element type has a
        /// JobHandle field named m_LaneHandle (the handle of the cascade's lane culling jobs). Collects every JobHandle
        /// field of that element type.
        /// </summary>
        private static FieldInfo FindCascadeCulling(Type ts, List<FieldInfo> handles)
        {
            foreach (FieldInfo f in ts.GetFields(k_Flags))
            {
                Type ft = f.FieldType;
                Type element = ft.IsArray ? ft.GetElementType()
                    : ft.IsGenericType && typeof(IList).IsAssignableFrom(ft) ? ft.GetGenericArguments()[0]
                    : null;
                if (element == null)
                    continue;

                FieldInfo lane = element.GetField("m_LaneHandle", k_Flags);
                if (lane == null || lane.FieldType != typeof(JobHandle))
                    continue;

                handles.Clear();
                foreach (FieldInfo ef in element.GetFields(k_Flags))
                {
                    if (ef.FieldType == typeof(JobHandle))
                        handles.Add(ef);
                }
                return f;
            }
            return null;
        }

        /// <summary>Comma-separated field names for the startup log.</summary>
        private static string Names(List<FieldInfo> fields)
        {
            var names = new List<string>(fields.Count);
            foreach (FieldInfo f in fields)
                names.Add(f.Name);
            return string.Join(",", names);
        }

        /// <summary>Replaces the overlay with the given lanes and redraws the old and new areas.</summary>
        public void Set(List<TerrainSystem.LaneSection> lanes, float4 area)
        {
            if (!Ok)
                return;

            if (lanes == null || lanes.Count == 0)
            {
                Clear();
                return;
            }

            CompleteJobs();
            RemoveBlock();

            float4 dirty = Active ? Union(m_Area, area) : area;
            m_Lanes.Clear();
            m_Lanes.AddRange(lanes);
            m_Area = area;

            Inject();
            MarkDirty(dirty);
        }

        /// <summary>Removes the overlay and redraws its area.</summary>
        public void Clear()
        {
            if (!Ok || !Active)
                return;

            CompleteJobs();
            RemoveBlock();
            MarkDirty(m_Area);
            m_Lanes.Clear();
        }

        /// <summary>Forgets injected lanes and pending refreshes without touching the terrain (used after a map load).</summary>
        public void Reset()
        {
            m_Lanes.Clear();
            m_RefreshCountdown = 0;
        }

        /// <summary>Re-injects the overlay if the game rebuilt its lane list. Call once per frame.</summary>
        public void KeepAlive()
        {
            if (!Ok || !Active)
                return;

            FramesSinceInject++;
            CompleteJobs();
            if (!Present())
            {
                Inject();
                MarkDirty(m_Area);
            }
        }

        /// <summary>Re-renders the last changed area once the overlay has been still for a few frames. Call once per frame.</summary>
        public void Tick()
        {
            if (m_RefreshCountdown > 0 && --m_RefreshCountdown == 0)
                RequestRedraw(m_RefreshArea);
        }

        /// <summary>
        /// Redraws the area now and schedules one delayed redraw: the terrain splatmap is rebuilt from the
        /// cascade one frame behind, so without the second pass it keeps the overlay's slope colors.
        /// </summary>
        private void MarkDirty(float4 area)
        {
            RequestRedraw(area);
            m_RefreshArea = m_RefreshCountdown > 0 ? Union(m_RefreshArea, area) : area;
            m_RefreshCountdown = k_SplatRefreshDelay;
        }

        /// <summary>
        /// Redraws the terrain cascades over the area without notifying GroundHeightSystem (render only). Falls back to
        /// the full OnAreaChanged if a game update renamed the field.
        /// </summary>
        private void RequestRedraw(float4 area)
        {
            if (m_UpdateAreaField == null)
            {
                m_Terrain.OnAreaChanged(area);
                return;
            }

            float4 current = (float4)m_UpdateAreaField.GetValue(m_Terrain);
            current = math.lengthsq(current) > 0f
                ? new float4(math.min(current.xy, area.xy), math.max(current.zw, area.zw))
                : area;
            m_UpdateAreaField.SetValue(m_Terrain, current);
        }

        /// <summary>Returns a view of TerrainSystem's lane list sharing the same native memory.</summary>
        private NativeList<TerrainSystem.LaneSection> LaneList() =>
            (NativeList<TerrainSystem.LaneSection>)m_LaneListField.GetValue(m_Terrain);

        /// <summary>Appends the overlay lanes to the end of the lane list.</summary>
        private void Inject()
        {
            NativeList<TerrainSystem.LaneSection> list = LaneList();
            m_Base = list.Length;
            foreach (TerrainSystem.LaneSection lane in m_Lanes)
                list.Add(lane);
            m_Count = m_Lanes.Count;
            m_First = m_Lanes[0];
            FramesSinceInject = 0;
        }

        /// <summary>Checks whether the injected block is still at the end of the lane list.</summary>
        private bool Present()
        {
            if (m_Base < 0 || m_Count == 0)
                return false;

            NativeList<TerrainSystem.LaneSection> list = LaneList();
            if (list.Length != m_Base + m_Count)
                return false;

            TerrainSystem.LaneSection l = list[m_Base];
            return math.all(l.m_Left.c0 == m_First.m_Left.c0)
                && math.all(l.m_Right.c2 == m_First.m_Right.c2)
                && math.all(l.m_Bounds.min == m_First.m_Bounds.min);
        }

        /// <summary>Truncates the injected block from the lane list if it is still present.</summary>
        private void RemoveBlock()
        {
            if (Present())
            {
                NativeList<TerrainSystem.LaneSection> list = LaneList();
                list.Length = m_Base;
            }

            m_Base = -1;
            m_Count = 0;
        }

        /// <summary>
        /// Completes every TerrainSystem job that reads or writes the lane list, including each cascade's lane culling
        /// jobs, before the list is touched on the main thread.
        /// </summary>
        private void CompleteJobs()
        {
            foreach (FieldInfo f in m_JobFields)
                ((JobHandle)f.GetValue(m_Terrain)).Complete();

            // Null before the first cull: no cascade job has been scheduled yet.
            if (m_CascadeCullingField.GetValue(m_Terrain) is IList cascades)
            {
                foreach (object info in cascades)
                {
                    if (info == null)
                        continue;
                    foreach (FieldInfo f in m_CascadeJobFields)
                        ((JobHandle)f.GetValue(info)).Complete();
                }
            }
        }

        /// <summary>Returns the bounding rectangle of two (minX, minZ, maxX, maxZ) areas.</summary>
        private static float4 Union(float4 a, float4 b) => new(math.min(a.xy, b.xy), math.max(a.zw, b.zw));
    }

    internal sealed class BaseHeightCache
    {
        private const int k_MarginTexels = 128;

        private readonly TerrainSystem m_Terrain;
        private float[] m_Data;
        private int4 m_Rect;
        private int2 m_TexSize;
        private float2 m_Area;
        private float2 m_Offset;

        public bool Valid { get; private set; }

        /// <summary>World size of one base heightmap texel in meters, or 0 if unavailable.</summary>
        public float TexelSize
        {
            get
            {
                Texture tex = m_Terrain.heightmap;
                return tex == null || tex.width <= 0 ? 0f : m_Terrain.playableArea.x / tex.width;
            }
        }

        /// <summary>Creates an empty cache for the given terrain system.</summary>
        public BaseHeightCache(TerrainSystem terrain)
        {
            m_Terrain = terrain;
        }

        /// <summary>Marks the cache as stale so the next Ensure reads from the GPU.</summary>
        public void Invalidate() => Valid = false;

        /// <summary>Ensures base heights covering the given world rectangle are cached, reading from the GPU if needed.</summary>
        public bool Ensure(float2 min, float2 max)
        {
            Texture tex = m_Terrain.heightmap;
            if (tex == null)
                return false;

            float2 area = m_Terrain.playableArea;
            float2 off = m_Terrain.playableOffset;
            if (math.any(area <= 0f))
                return false;

            int2 size = new(tex.width, tex.height);
            int2 p0 = math.clamp((int2)math.floor((min - off) / area * (float2)size) - 2, int2.zero, size - 1);
            int2 p1 = math.clamp((int2)math.ceil((max - off) / area * (float2)size) + 2, int2.zero, size - 1);

            if (Valid && math.all(size == m_TexSize) && math.all(area == m_Area) && math.all(off == m_Offset)
                && math.all(p0 >= m_Rect.xy) && math.all(p1 < m_Rect.xy + m_Rect.zw))
                return true;

            int2 r0 = math.max(p0 - k_MarginTexels, int2.zero);
            int2 r1 = math.min(p1 + k_MarginTexels, size - 1);
            int w = r1.x - r0.x + 1;
            int h = r1.y - r0.y + 1;

            AsyncGPUReadbackRequest req = AsyncGPUReadback.Request(tex, 0, r0.x, w, r0.y, h, 0, 1);
            req.WaitForCompletion();
            if (req.hasError || !req.done)
            {
                Valid = false;
                return false;
            }

            NativeArray<ushort> raw = req.GetData<ushort>();
            if (raw.Length < w * h)
            {
                Valid = false;
                return false;
            }

            float scale = m_Terrain.heightScaleOffset.x / 65535f;
            float offY = m_Terrain.positionOffset.y;
            m_Data = new float[w * h];
            for (int k = 0; k < m_Data.Length; k++)
                m_Data[k] = raw[k] * scale + offY;

            m_Rect = new int4(r0, w, h);
            m_TexSize = size;
            m_Area = area;
            m_Offset = off;
            Valid = true;
            return true;
        }

        /// <summary>Mirrors a Level brush (lerp toward target by mask) into the cache to avoid a blocking re-read.</summary>
        public void ApplyLevel(float2 origin, float size, int res, Color[] mask, float target)
        {
            if (!Valid)
                return;

            float2 texel = m_Area / (float2)m_TexSize;
            int2 lo = math.max((int2)math.floor((origin - m_Offset) / texel) - m_Rect.xy - 1, int2.zero);
            int2 hi = math.min((int2)math.ceil((origin + size - m_Offset) / texel) - m_Rect.xy + 1, m_Rect.zw - 1);
            for (int y = lo.y; y <= hi.y; y++)
            {
                for (int x = lo.x; x <= hi.x; x++)
                {
                    float2 world = m_Offset + (new float2(m_Rect.x + x, m_Rect.y + y) + 0.5f) * texel;
                    float2 uv = (world - origin) / size * res - 0.5f;
                    if (uv.x < -0.5f || uv.y < -0.5f || uv.x > res - 0.5f || uv.y > res - 0.5f)
                        continue;

                    uv = math.clamp(uv, 0f, res - 1.001f);
                    int2 i0 = (int2)math.floor(uv);
                    float2 f = uv - i0;
                    int k = i0.y * res + i0.x;
                    float a = math.lerp(mask[k].r, mask[k + 1].r, f.x);
                    float b = math.lerp(mask[k + res].r, mask[k + res + 1].r, f.x);
                    float w = math.lerp(a, b, f.y);
                    if (w <= 0f)
                        continue;

                    int idx = y * m_Rect.z + x;
                    m_Data[idx] = math.lerp(m_Data[idx], target, w);
                }
            }
        }

        /// <summary>Returns true if the world position lies inside the cached region.</summary>
        public bool Contains(float2 world)
        {
            if (!Valid)
                return false;

            float2 t = (world - m_Offset) / m_Area * (float2)m_TexSize - (float2)m_Rect.xy;
            return t.x >= 0f && t.y >= 0f && t.x <= m_Rect.z - 1 && t.y <= m_Rect.w - 1;
        }

        /// <summary>Bilinearly samples the cached base height at a world position (clamped to the cached region).</summary>
        public float Sample(float2 world)
        {
            float2 t = (world - m_Offset) / m_Area * (float2)m_TexSize - 0.5f - (float2)m_Rect.xy;
            int w = m_Rect.z, h = m_Rect.w;
            t = math.clamp(t, float2.zero, new float2(w - 1.001f, h - 1.001f));
            int2 i0 = (int2)math.floor(t);
            float2 f = t - i0;
            int k = i0.y * w + i0.x;
            float a = math.lerp(m_Data[k], m_Data[k + 1], f.x);
            float b = math.lerp(m_Data[k + w], m_Data[k + w + 1], f.x);
            return math.lerp(a, b, f.y);
        }
    }

    internal static class FlattenFootprint
    {
        public const int CircleSamples = 24;
        public const float LaneProfileSteepness = 1.25f;
        private const float k_MinHullEdge = 2f;
        private const float k_RingWidth = 2f;
        private const float k_SlabOverlap = 0.25f;
        private static readonly float[] s_SlopeProbe = { 0f, 4f, 8f, 16f, 32f };

        /// <summary>Builds a CCW convex hull around circles of radius r centered on the given points.</summary>
        public static void BuildHull(List<float2> centers, float r, List<float2> hull)
        {
            var points = new List<float2>(centers.Count * CircleSamples);
            foreach (float2 p in centers)
            {
                for (int c = 0; c < CircleSamples; c++)
                {
                    float a = c * (2f * math.PI / CircleSamples);
                    points.Add(p + r * new float2(math.cos(a), math.sin(a)));
                }
            }

            ConvexHull(points, hull);
            Decimate(hull, k_MinHullEdge);
        }

        /// <summary>Returns the average of the given points.</summary>
        public static float2 Centroid(List<float2> poly)
        {
            float2 c = float2.zero;
            foreach (float2 p in poly)
                c += p;
            return c / math.max(1, poly.Count);
        }

        /// <summary>Returns the axis-aligned bounds of the given points.</summary>
        public static void MinMax(List<float2> poly, out float2 min, out float2 max)
        {
            min = new float2(float.MaxValue);
            max = new float2(float.MinValue);
            foreach (float2 p in poly)
            {
                min = math.min(min, p);
                max = math.max(max, p);
            }
        }

        /// <summary>
        /// Builds the overlay lanes: a C1-continuous ring with an outward slope around the hull and
        /// flat interior slabs. The slope width is uniform and sized so the slope never exceeds maxSlope.
        /// </summary>
        public static void BuildLanes(List<float2> hull, float y, Func<float2, float> baseHeight,
            float maxSlope, float minWidth, float maxWidth, List<TerrainSystem.LaneSection> lanes,
            out float usedWidth)
        {
            lanes.Clear();

            float perimeter = 0f;
            for (int i = 0; i < hull.Count; i++)
                perimeter += math.distance(hull[i], hull[(i + 1) % hull.Count]);

            List<float2> outer = ResampleClosed(hull, math.clamp(perimeter / 48f, 4f, 12f));
            int n = outer.Count;

            var inner = new List<float2>(n);
            float width = minWidth;
            for (int i = 0; i < n; i++)
            {
                float2 dir = math.normalizesafe(outer[(i + 1) % n] - outer[(i - 1 + n) % n]);
                float2 leftNormal = new(-dir.y, dir.x);
                inner.Add(outer[i] + leftNormal * k_RingWidth);

                float dh = 0f;
                foreach (float s in s_SlopeProbe)
                    dh = math.max(dh, math.abs(y - baseHeight(outer[i] - leftNormal * s)));
                width = math.max(width, math.clamp(LaneProfileSteepness * dh / maxSlope, minWidth, maxWidth));
            }

            usedWidth = width;
            for (int i = 0; i < n; i++)
            {
                CatmullRom(outer, i, y, out float3 oa, out float3 ob, out float3 oc, out float3 od);
                CatmullRom(inner, i, y, out float3 ia, out float3 ib, out float3 ic, out float3 id);
                lanes.Add(MakeLane(ia, ib, ic, id, oa, ob, oc, od, width));
            }

            var xs = new List<float>(hull.Count);
            foreach (float2 p in hull)
                xs.Add(p.x);
            xs.Sort();

            for (int i = 0; i + 1 < xs.Count; i++)
            {
                float x0 = xs[i];
                float x1 = xs[i + 1];
                if (x1 - x0 < 0.05f)
                    continue;

                ZRange(hull, x0 + 0.001f, out float lo0, out float hi0);
                ZRange(hull, x1 - 0.001f, out float lo1, out float hi1);
                float xa = x0 - k_SlabOverlap;
                float xb = x1 + k_SlabOverlap;

                float3 la = new(xa, y, hi0), ld = new(xb, y, hi1);
                float3 ra = new(xa, y, lo0), rd = new(xb, y, lo1);
                lanes.Add(MakeLane(la, math.lerp(la, ld, 1f / 3f), math.lerp(la, ld, 2f / 3f), ld,
                                   ra, math.lerp(ra, rd, 1f / 3f), math.lerp(ra, rd, 2f / 3f), rd, 0f));
            }
        }

        /// <summary>Creates a terrain-shifting lane that clamps the ground exactly to the curve height.</summary>
        private static TerrainSystem.LaneSection MakeLane(float3 la, float3 lb, float3 lc, float3 ld,
                                                           float3 ra, float3 rb, float3 rc, float3 rd, float width)
        {
            float2 min = math.min(math.min(math.min(la.xz, lb.xz), math.min(lc.xz, ld.xz)), math.min(math.min(ra.xz, rb.xz), math.min(rc.xz, rd.xz)));
            float2 max = math.max(math.max(math.max(la.xz, lb.xz), math.max(lc.xz, ld.xz)), math.max(math.max(ra.xz, rb.xz), math.max(rc.xz, rd.xz)));

            return new TerrainSystem.LaneSection
            {
                m_Bounds = new Bounds2(min - width - 1f, max + width + 1f),
                m_Left = Rows(la, lb, lc, ld),
                m_Right = Rows(ra, rb, rc, rd),
                m_MinOffset = float3.zero,
                m_MaxOffset = float3.zero,
                m_ClipOffset = float2.zero,
                m_WidthOffset = width,
                m_MiddleSize = 0.5f,
                m_Flags = TerrainSystem.LaneFlags.ShiftTerrain,
            };
        }

        /// <summary>Packs four Bezier control points into the row layout used by LaneSection.</summary>
        private static float4x3 Rows(float3 a, float3 b, float3 c, float3 d) =>
            new(a.x, a.y, a.z, b.x, b.y, b.z, c.x, c.y, c.z, d.x, d.y, d.z);

        /// <summary>Returns the cubic Bezier of the closed Catmull-Rom span starting at point i.</summary>
        private static void CatmullRom(List<float2> p, int i, float y, out float3 a, out float3 b, out float3 c, out float3 d)
        {
            int n = p.Count;
            float2 p0 = p[(i - 1 + n) % n];
            float2 p1 = p[i];
            float2 p2 = p[(i + 1) % n];
            float2 p3 = p[(i + 2) % n];
            float2 b2 = p1 + (p2 - p0) / 6f;
            float2 c2 = p2 - (p3 - p1) / 6f;
            a = new float3(p1.x, y, p1.y);
            b = new float3(b2.x, y, b2.y);
            c = new float3(c2.x, y, c2.y);
            d = new float3(p2.x, y, p2.y);
        }

        /// <summary>Returns the z range of a convex polygon at the given x.</summary>
        private static void ZRange(List<float2> hull, float x, out float lo, out float hi)
        {
            lo = float.MaxValue;
            hi = float.MinValue;
            for (int i = 0; i < hull.Count; i++)
            {
                float2 a = hull[i];
                float2 b = hull[(i + 1) % hull.Count];
                float minX = math.min(a.x, b.x), maxX = math.max(a.x, b.x);
                if (x < minX || x > maxX)
                    continue;
                if (maxX - minX < 1e-4f)
                {
                    lo = math.min(lo, math.min(a.y, b.y));
                    hi = math.max(hi, math.max(a.y, b.y));
                }
                else
                {
                    float z = math.lerp(a.y, b.y, (x - a.x) / (b.x - a.x));
                    lo = math.min(lo, z);
                    hi = math.max(hi, z);
                }
            }
            if (lo > hi)
                lo = hi = 0f;
        }

        /// <summary>Resamples a closed polygon into evenly spaced points along its perimeter.</summary>
        private static List<float2> ResampleClosed(List<float2> poly, float spacing)
        {
            float perimeter = 0f;
            for (int i = 0; i < poly.Count; i++)
                perimeter += math.distance(poly[i], poly[(i + 1) % poly.Count]);

            int n = math.max(12, (int)math.ceil(perimeter / spacing));
            float step = perimeter / n;
            var result = new List<float2>(n);

            int edge = 0;
            float edgeStart = 0f;
            float edgeLen = math.distance(poly[0], poly[1 % poly.Count]);
            for (int k = 0; k < n; k++)
            {
                float s = k * step;
                while (s > edgeStart + edgeLen && edge < poly.Count - 1)
                {
                    edgeStart += edgeLen;
                    edge++;
                    edgeLen = math.distance(poly[edge], poly[(edge + 1) % poly.Count]);
                }
                float t = edgeLen > 1e-4f ? math.saturate((s - edgeStart) / edgeLen) : 0f;
                result.Add(math.lerp(poly[edge], poly[(edge + 1) % poly.Count], t));
            }
            return result;
        }

        /// <summary>Computes a CCW convex hull (Andrew's monotone chain).</summary>
        private static void ConvexHull(List<float2> pts, List<float2> hull)
        {
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            hull.Clear();
            var h = new float2[pts.Count * 2];
            int k = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                while (k >= 2 && Cross(h[k - 2], h[k - 1], pts[i]) <= 0f) k--;
                h[k++] = pts[i];
            }
            for (int i = pts.Count - 2, t = k + 1; i >= 0; i--)
            {
                while (k >= t && Cross(h[k - 2], h[k - 1], pts[i]) <= 0f) k--;
                h[k++] = pts[i];
            }
            for (int i = 0; i < k - 1; i++)
                hull.Add(h[i]);
        }

        /// <summary>Returns the 2D cross product of (a - o) and (b - o).</summary>
        private static float Cross(float2 o, float2 a, float2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        /// <summary>Removes hull vertices closer than minEdge to the previously kept vertex.</summary>
        private static void Decimate(List<float2> hull, float minEdge)
        {
            if (hull.Count <= 8)
                return;
            var kept = new List<float2> { hull[0] };
            for (int i = 1; i < hull.Count; i++)
            {
                if (math.distance(hull[i], kept[kept.Count - 1]) >= minEdge)
                    kept.Add(hull[i]);
            }
            if (kept.Count >= 3 && math.distance(kept[kept.Count - 1], kept[0]) < minEdge)
                kept.RemoveAt(kept.Count - 1);
            if (kept.Count >= 3)
            {
                hull.Clear();
                hull.AddRange(kept);
            }
        }
    }
}
