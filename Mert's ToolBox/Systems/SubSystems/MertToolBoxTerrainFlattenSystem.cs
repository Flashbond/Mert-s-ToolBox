using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using MertsToolBox.Management;
using MertsToolBox.Systems.SubSystems;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Color = UnityEngine.Color;
using GameNetConnectedEdge = Game.Net.ConnectedEdge;
using GameNetCurve = Game.Net.Curve;
using GameNetEdge = Game.Net.Edge;
using GameNetElevation = Game.Net.Elevation;
using GameNetNode = Game.Net.Node;

namespace MertsToolBox.Systems
{
    [UpdateAfter(typeof(ObjectToolSystem))]
    public partial class MertToolBoxTerrainFlattenSystem : SystemBase
    {
        private const float k_FlatMargin = 4f;
        private const float k_MaxSlope = 0.30f;
        private const float k_MinFalloff = 4f;
        private const float k_MaxFalloff = 64f;
        private const int k_MaskResolution = 512;
        private const int k_CurveSamples = 24;
        private const int k_PreviewCurveSamples = 8;
        private const int k_MaxWaitFrames = 15;
        private const int k_RefreshDelayFrames = 12;
        private const int k_BakeSettleFrames = 8;
        private const int k_BakeMaxWaitFrames = 120;
        private const float k_PreviewMoveEpsilon = 0.25f;
        private const int k_PreviewMinFrames = 2;
        private const int k_PreviewGraceFrames = 15;
        private const float k_RaycastSearch = 250f;
        private const float k_TerrainHitTolerance = 0.75f;
        private const float k_WaterFreeboard = 1f;       // flat ground never ends up closer than this above water
        private const float k_MaxWaterDepth = 200f;      // sanity limit for a sampled water depth
        private const int k_TargetLogMax = 300;          // DEBUG: [FLATTEN-TARGET] line limit per session

        // "Parting the sea": while the stamp moves, the flattened footprint is grown so the ground ahead of it is
        // already flat (and already in the game's CPU height copy) when the stamp arrives. Once it has been still for
        // a few frames the footprint shrinks back to the stamp's real size.
        private const float k_ExpandFactor = 0.25f;      // extra margin per side = 25% of the stamp extent (~x1.5)
        private const float k_ExpandMin = 8f;
        private const float k_ExpandMax = 40f;
        private const int k_ShrinkAfterStillFrames = 8;
        private const float k_MouseRayTolerance = 3f;

        private ToolSystem m_ToolSystem;
        private ObjectToolSystem m_ObjectToolSystem;
        private PrefabSystem m_PrefabSystem;
        private TerrainSystem m_TerrainSystem;
        private WaterSystem m_WaterSystem;
        private Game.Rendering.CameraUpdateSystem m_CameraUpdateSystem;

        internal static MertToolBoxTerrainFlattenSystem Instance { get; private set; }

        private static readonly System.Reflection.FieldInfo s_ObjectToolForceUpdate =
            typeof(ObjectToolSystem).GetField("m_ForceUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private EntityQuery m_TempNodeQuery;
        private EntityQuery m_TempEdgeQuery;
        private EntityQuery m_CourseDefinitionQuery;
        private readonly HashSet<int2> m_TmpNodeKeys = new();

        private Texture2D m_MaskTexture;

        private sealed class PendingBatch
        {
            public readonly List<Entity> Nodes = new();
            public readonly List<Entity> Edges = new();
            public float RoadY;
            public float Elevation;
            public float HalfWidth;
            public int WaitedFrames;
            public bool Applied;
            public int FramesSinceApply;
            public bool UsesOverlay;
            public List<float2> Hull;
            public float MaxWidth;
            public bool NetworkDone;
            public bool Baked;
            public int BakeWait;
        }

        private readonly List<PendingBatch> m_Pending = new();

        private TerrainLaneOverlay m_Overlay;
        private BaseHeightCache m_Base;
        private bool m_PreviewValid;
        private float m_PreviewTarget;
        private float m_LastLoggedTarget = -1e9f;
        private int m_TargetLogLines;
        private float m_PreviewMaxWidth;
        private readonly List<float2> m_PreviewHull = new();
        private float2 m_SigCentroid;
        private float2 m_SigMin;
        private float2 m_SigMax;
        private float2 m_SeenCentroid;
        private float2 m_SeenMin;
        private float2 m_SeenMax;
        private int m_StillFrames;
        private bool m_BuiltExpanded;
        private float m_BuiltExpand;
        private int m_FramesSinceBuild;
        private int m_MissingFrames;
        private readonly List<float2> m_TmpCenters = new();
        private readonly List<float2> m_TmpNodes = new();
        private readonly List<TerrainSystem.LaneSection> m_TmpLanes = new();

        /// <summary>Caches systems, creates the Temp net queries and the preview helpers.</summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_WaterSystem = World.GetOrCreateSystemManaged<WaterSystem>();
            m_CameraUpdateSystem = World.GetOrCreateSystemManaged<Game.Rendering.CameraUpdateSystem>();

            m_TempNodeQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<GameNetNode>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>());

            // The object tool's net definitions for the stamp's sub-roads: the footprint is known as soon as the tool
            // writes them, without waiting for the game to generate the temp preview roads from them.
            m_CourseDefinitionQuery = GetEntityQuery(
                ComponentType.ReadOnly<CreationDefinition>(),
                ComponentType.ReadOnly<NetCourse>(),
                ComponentType.Exclude<Deleted>());

            m_TempEdgeQuery = GetEntityQuery(
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<GameNetEdge>(),
                ComponentType.ReadOnly<GameNetCurve>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>());

            m_Overlay = new TerrainLaneOverlay(m_TerrainSystem);
            m_Base = new BaseHeightCache(m_TerrainSystem);
            Instance = this;

            GameManager.instance.onGameLoadingComplete += OnGameLoadingComplete;
        }

        /// <summary>Releases the mask texture.</summary>
        protected override void OnDestroy()
        {
            if (GameManager.instance != null)
            {
                GameManager.instance.onGameLoadingComplete -= OnGameLoadingComplete;
            }

            if (Instance == this)
                Instance = null;

            if (m_MaskTexture != null)
            {
                UnityEngine.Object.Destroy(m_MaskTexture);
                m_MaskTexture = null;
            }

            base.OnDestroy();
        }

        /// <summary>Runs only while a flatten-capable tool owns the session or earlier work is still in flight.</summary>
        protected override void OnUpdate()
        {
            bool wanted = MertToolState.FlattenToolActive && MertToolState.FlattenGeometryEnabled;
            if (!wanted && !MertToolState.FlattenBusy)
                return;

            m_Overlay.Tick();

            if (!MertToolState.FlattenGeometryEnabled)
            {
                CancelAll();
                PublishBusyState();
                return;
            }
            
            for (int i = m_Pending.Count - 1; i >= 0; i--)
            {
                if (TryProcessPending(m_Pending[i]))
                    m_Pending.RemoveAt(i);
            }

            bool stampActive = IsStampToolActive(m_ToolSystem, m_ObjectToolSystem);
            bool applying = stampActive && m_ObjectToolSystem.applyMode == ApplyMode.Apply;

            bool holding = m_Pending.Exists(b => b.UsesOverlay && !b.Baked);
            // The regular preview is rebuilt late in the frame (LatePreviewUpdate) from this frame's definitions.
            // Only the commit frame rebuilds here, unthrottled, so the captured batch matches what is being built.
            if (!holding && applying)
                UpdatePreview(true);
            else if (!holding && m_PreviewValid && !IsFlattenStampPreview(out _))
                StopPreview();   // the tool closed or switched away: the late pass no longer runs, remove the preview here
            m_Overlay.KeepAlive();

            if (applying && TryCaptureBatch(m_PrefabSystem.GetEntity(MertBaseToolSystem.SharedRuntimeStamp), out PendingBatch batch))
            {
                if (!holding && m_PreviewValid && m_Overlay.Active)
                {
                    batch.UsesOverlay = true;
                    batch.Hull = new List<float2>(m_PreviewHull);
                    batch.MaxWidth = m_PreviewMaxWidth;
                    batch.RoadY = m_PreviewTarget + batch.Elevation;
                }

                m_Pending.Add(batch);
            }

            PublishBusyState();
        }

        /// <summary>
        /// Called by MertFlattenLatePreviewSystem at the end of the modification phases: the object tool's definitions
        /// of THIS frame are there, and the overlay injected now is drawn in this same frame.
        /// </summary>
        internal void LatePreviewUpdate()
        {
            if (!MertToolState.FlattenToolActive || !MertToolState.FlattenGeometryEnabled || !m_Overlay.Ok)
                return;

            if (m_Pending.Exists(b => b.UsesOverlay && !b.Baked))
                return;

            UpdatePreview(false);
            PublishBusyState();
        }

        /// <summary>
        /// Asks the object tool to rebuild its preview on its next update (same as its own m_ForceUpdate). Used when the
        /// flat road height changed while the tool's definitions stayed the same (e.g. the footprint shrank back after a
        /// move): MertFlattenCourseSystem then flattens the fresh definitions to the new height.
        /// </summary>
        private void ForceObjectToolRefresh()
        {
            try
            {
                if (s_ObjectToolForceUpdate != null && s_ObjectToolForceUpdate.FieldType == typeof(bool))
                    s_ObjectToolForceUpdate.SetValue(m_ObjectToolSystem, true);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Height the stamp's roads must have while the flatten preview is shown: the overlay's flat ground plus the
        /// tool elevation - the same value the commit uses (batch.RoadY). False when no flatten preview is active.
        /// </summary>
        internal bool TryGetPreviewRoadHeight(out float roadY, out float elevation, out HashSet<Entity> roadPrefabs)
        {
            roadY = 0f;
            elevation = MertBaseToolSystem.LastHandoffElevation;
            roadPrefabs = null;

            if (!m_PreviewValid || !m_Overlay.Active || !IsFlattenStampPreview(out AssetStampPrefab stamp))
                return false;
            if (!GetStampRoadPrefabs(m_PrefabSystem.GetEntity(stamp), out roadPrefabs, out _))
                return false;

            roadY = m_PreviewTarget + elevation;
            return true;
        }

        /// <summary>Drops every state tied to the previous map.</summary>
        private void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            m_Pending.Clear();
            m_Overlay.Reset();
            m_Base.Invalidate();
            m_PreviewHull.Clear();
            m_PreviewValid = false;
            m_MissingFrames = 0;
            MertToolState.FlattenBusy = false;
        }


        /// <summary>Drops every preview, overlay and unbaked batch; the overlay's delayed splat refresh still completes.</summary>
        private void CancelAll()
        {
            if (m_Pending.Count == 0 && !m_Overlay.Active && !m_PreviewValid)
                return;

            m_Pending.Clear();
            StopPreview();
        }

        /// <summary>Publishes whether this system still needs to run next frame.</summary>
        private void PublishBusyState()
        {
            MertToolState.FlattenBusy = m_Pending.Count > 0 || m_Overlay.Active || m_PreviewValid || m_Overlay.RefreshPending;
        }

        /// <summary>
        /// Moves an object-tool terrain hit that landed on the preview overlay to where the same camera ray meets
        /// the original terrain, so the overlay never feeds back into the stamp position.
        /// </summary>
        internal bool TryCorrectRaycast(ToolBaseSystem tool, ref Game.Common.RaycastHit hit)
        {
            if (tool != m_ObjectToolSystem || !m_Overlay.Active || !m_Base.Valid || !IsStampToolActive(m_ToolSystem, m_ObjectToolSystem))
                return false;

            float3 p = hit.m_HitPosition;
            float4 area = m_Overlay.Area;
            if (p.x < area.x || p.z < area.y || p.x > area.z || p.z > area.w)
                return false;

            TerrainHeightData heightData = m_TerrainSystem.GetHeightData(false);
            if (!heightData.isCreated || math.abs(TerrainUtils.SampleHeight(ref heightData, p) - p.y) > k_TerrainHitTolerance)
                return false;

            // Walk the CURRENT mouse ray, not the direction towards the game's hit point: the game's terrain hit is
            // only approximately on the ray and moves a little every time the overlay is rebuilt, which made the
            // corrected point settle ~0.7 m away after each move (visible as the stamp twitching). The mouse ray does
            // not depend on the overlay at all, so a still mouse always gives the same stamp position.
            if (!TryGetMouseRay(p, out float3 origin, out float3 dir))
            {
                origin = m_CameraUpdateSystem.position;
                float3 toHitFallback = p - origin;
                float lenFallback = math.length(toHitFallback);
                if (lenFallback < 1f)
                    return false;
                dir = toHitFallback / lenFallback;
            }

            float dist = math.dot(p - origin, dir);
            if (dist < 1f)
                return false;
            float step = math.max(1f, m_Base.TexelSize * 0.5f);
            float t0 = math.max(0f, dist - k_RaycastSearch);
            float t1 = dist + k_RaycastSearch;

            float prevT = -1f;
            for (float t = t0; t <= t1; t += step)
            {
                float3 q = origin + dir * t;
                if (!m_Base.Contains(q.xz))
                {
                    if (prevT >= 0f)
                        return false;
                    continue;
                }

                float f = q.y - m_Base.Sample(q.xz);
                if (f > 0f)
                {
                    prevT = t;
                    continue;
                }

                if (prevT < 0f)
                    return false;

                float lo = prevT, hi = t;
                for (int i = 0; i < 10; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (AboveBase(origin, dir, mid) > 0f)
                        lo = mid;
                    else
                        hi = mid;
                }

                float tHit = (lo + hi) * 0.5f;
                float3 np = origin + dir * tHit;
                hit.m_HitPosition = np;
                hit.m_Position = np;
                hit.m_NormalizedDistance *= tHit / dist;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The camera ray under the mouse cursor. Returns false (caller falls back to the hit direction) when there is no
        /// camera or mouse, or when the ray passes too far from the game's hit (e.g. the camera moved since the raycast).
        /// </summary>
        private static bool TryGetMouseRay(float3 hitPoint, out float3 origin, out float3 dir)
        {
            origin = default;
            dir = default;

            Camera cam = Camera.main;
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (cam == null || mouse == null)
                return false;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            origin = ray.origin;
            dir = math.normalizesafe((float3)ray.direction);
            if (math.all(dir == float3.zero))
                return false;

            // Sanity check: the game's hit should lie close to this ray.
            float3 toHit = hitPoint - origin;
            float along = math.dot(toHit, dir);
            return along > 1f && math.length(toHit - dir * along) < k_MouseRayTolerance;
        }

        /// <summary>Returns the height of the ray point at distance t above the cached original terrain.</summary>
        private float AboveBase(float3 origin, float3 dir, float t)
        {
            float3 q = origin + dir * t;
            return q.y - m_Base.Sample(q.xz);
        }

        /// <summary>Returns true while the object tool holds the shared runtime stamp of any shape tool.</summary>
        internal static bool IsStampToolActive(ToolSystem toolSystem, ObjectToolSystem objectTool)
        {
            if (toolSystem.activeTool != objectTool)
                return false;

            AssetStampPrefab stamp = MertBaseToolSystem.SharedRuntimeStamp;
            return stamp != null && ReferenceEquals(objectTool.GetPrefab(), stamp);
        }

        /// <summary>Returns true while the object tool is previewing our flatten-capable stamp.</summary>
        private bool IsFlattenStampPreview(out AssetStampPrefab stamp)
        {
            stamp = MertBaseToolSystem.SharedRuntimeStamp;
            return MertToolState.FlattenGeometryEnabled
                && m_ToolSystem.activeTool == m_ObjectToolSystem
                && stamp != null
                && ReferenceEquals(m_ObjectToolSystem.GetPrefab(), stamp)
                && MertBaseToolSystem.LastHandoffSupportsFlatten;
        }

        /// <summary>Removes the preview overlay and resets the preview state.</summary>
        private void StopPreview()
        {
            if (m_Overlay.Active)
                m_Overlay.Clear();

            if (m_PreviewValid)
                m_Base.Invalidate();

            m_PreviewValid = false;
            m_MissingFrames = 0;
        }

        /// <summary>Rebuilds the preview overlay when the stamp moves; on the commit frame it rebuilds without throttling.</summary>
        private void UpdatePreview(bool commitFrame)
        {
            if (!m_Overlay.Ok || !IsFlattenStampPreview(out AssetStampPrefab stamp))
            {
                StopPreview();
                return;
            }

            m_FramesSinceBuild++;

            if (!GetStampRoadPrefabs(m_PrefabSystem.GetEntity(stamp), out HashSet<Entity> roadPrefabs, out float halfWidth))
            {
                StopPreview();
                return;
            }

            m_TmpCenters.Clear();
            m_TmpNodes.Clear();
            CollectFromDefinitions(roadPrefabs);

            if (m_TmpNodes.Count == 0 || m_TmpCenters.Count < 2)
            {
                if (m_Overlay.Active && ++m_MissingFrames <= k_PreviewGraceFrames)
                    return;
                StopPreview();
                return;
            }
            m_MissingFrames = 0;

            float2 centroid = FlattenFootprint.Centroid(m_TmpCenters);
            FlattenFootprint.MinMax(m_TmpCenters, out float2 cmin, out float2 cmax);

            // Still or moving? Compared with the previous frame (position and, through min/max, rotation).
            bool moved = math.distance(centroid, m_SeenCentroid) >= k_PreviewMoveEpsilon
                      || math.distance(cmin, m_SeenMin) >= k_PreviewMoveEpsilon
                      || math.distance(cmax, m_SeenMax) >= k_PreviewMoveEpsilon;
            m_SeenCentroid = centroid;
            m_SeenMin = cmin;
            m_SeenMax = cmax;
            m_StillFrames = moved ? 0 : m_StillFrames + 1;

            // The commit always flattens the real footprint; otherwise grow it until the stamp has settled.
            bool expanded = !commitFrame && m_StillFrames < k_ShrinkAfterStillFrames;
            float expand = expanded ? math.clamp(k_ExpandFactor * math.cmax(cmax - cmin), k_ExpandMin, k_ExpandMax) : 0f;

            if (m_PreviewValid && m_Overlay.Active && expanded == m_BuiltExpanded)
            {
                if (expanded)
                {
                    // Keep the grown overlay (and its height) while the stamp stays well inside it.
                    if (math.distance(centroid, m_SigCentroid) < m_BuiltExpand * 0.5f)
                        return;
                }
                else if (math.distance(centroid, m_SigCentroid) < k_PreviewMoveEpsilon
                         && math.distance(cmin, m_SigMin) < k_PreviewMoveEpsilon
                         && math.distance(cmax, m_SigMax) < k_PreviewMoveEpsilon)
                {
                    return;
                }
            }

            if (!commitFrame && m_PreviewValid && m_Overlay.Active && expanded == m_BuiltExpanded
                && m_FramesSinceBuild < k_PreviewMinFrames)
                return;

            float radius = halfWidth + k_FlatMargin + expand;
            float pad = radius + k_MaxFalloff + 40f;
            if (!m_Base.Ensure(cmin - pad, cmax + pad))
            {
                StopPreview();
                return;
            }

            float target = ComputeTargetHeight();

            FlattenFootprint.BuildHull(m_TmpCenters, radius, m_PreviewHull);
            if (m_PreviewHull.Count < 3)
            {
                StopPreview();
                return;
            }

            FlattenFootprint.BuildLanes(m_PreviewHull, target, m_Base.Sample, k_MaxSlope, k_MinFalloff, k_MaxFalloff,
                m_TmpLanes, out float maxWidth);
            FlattenFootprint.MinMax(m_PreviewHull, out float2 hmin, out float2 hmax);
            m_Overlay.Set(m_TmpLanes, new float4(hmin - maxWidth - 10f, hmax + maxWidth + 10f));

            // New flat height (or a new preview): the tool's current definitions were flattened to the old one.
            if (!commitFrame && (!m_PreviewValid || math.abs(target - m_PreviewTarget) > 0.01f))
                ForceObjectToolRefresh();

            m_PreviewValid = true;
            m_PreviewTarget = target;
            m_PreviewMaxWidth = maxWidth;
            m_SigCentroid = centroid;
            m_SigMin = cmin;
            m_SigMax = cmax;
            m_BuiltExpanded = expanded;
            m_BuiltExpand = expand;
            m_FramesSinceBuild = 0;
        }

        /// <summary>
        /// Footprint from the object tool's net definitions of the stamp (new courses of our road prefabs). Returns false
        /// when there are none yet (the preview then waits, see k_PreviewGraceFrames).
        /// </summary>
        private bool CollectFromDefinitions(HashSet<Entity> roadPrefabs)
        {
            if (m_CourseDefinitionQuery.IsEmptyIgnoreFilter)
                return false;

            m_TmpNodeKeys.Clear();
            using (NativeArray<Entity> defs = m_CourseDefinitionQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity e in defs)
                {
                    CreationDefinition definition = EntityManager.GetComponentData<CreationDefinition>(e);
                    if (definition.m_Original != Entity.Null || !roadPrefabs.Contains(definition.m_Prefab))
                        continue;

                    Bezier4x3 b = EntityManager.GetComponentData<NetCourse>(e).m_Curve;
                    for (int s = 0; s <= k_PreviewCurveSamples; s++)
                        m_TmpCenters.Add(MathUtils.Position(b, s / (float)k_PreviewCurveSamples).xz);

                    AddNodeOnce(b.a.xz);
                    AddNodeOnce(b.d.xz);
                }
            }

            return m_TmpNodes.Count > 0 && m_TmpCenters.Count >= 2;
        }

        /// <summary>
        /// Flat ground height for the current footprint, kept out of the water:
        ///  - only "dry" nodes (original ground above the water surface there) are averaged, so sea-floor samples at a
        ///    coast no longer drag the height below the water line,
        ///  - the result never goes below the highest water surface under the footprint plus a freeboard; otherwise the
        ///    water simulation floods the lowered ground and, while moving inland, the "sea" follows the stamp,
        ///  - a footprint entirely in water gets water surface + freeboard (the water is filled in).
        /// Water surface = max(sea level, ORIGINAL ground + water depth) at each node.
        /// The surface must not be taken from WaterUtils.SampleHeight directly: that adds the depth to the game's CPU
        /// height copy, which already contains our own preview overlay. When the overlay lifts the ground, the water
        /// column standing there is lifted with it for a while, so "overlay height + depth + freeboard" became the next
        /// target, the overlay rose again, and the stamp climbed without end. The original ground (base heightmap
        /// cache, never touched by the overlay) plus the depth cannot exceed the real water column, so no feedback.
        /// The result is finally clamped to the terrain's height range as a last safety.
        /// </summary>
        private float ComputeTargetHeight()
        {
            float seaLevel = m_WaterSystem.SeaLevel;
            bool haveWaterData = false;
            WaterSurfaceData<SurfaceWater> water = default;
            TerrainHeightData terrain = default;
            try
            {
                water = m_WaterSystem.GetSurfaceData(out JobHandle deps);
                deps.Complete();
                terrain = m_TerrainSystem.GetHeightData(false);
                haveWaterData = terrain.isCreated;
            }
            catch
            {
                haveWaterData = false;
            }

            float drySum = 0f;
            int dryCount = 0;
            float allSum = 0f;
            float maxWaterTop = float.MinValue;
            bool anyWet = false;

            foreach (float2 n in m_TmpNodes)
            {
                float ground = m_Base.Sample(n);
                allSum += ground;

                float waterTop = seaLevel;
                if (haveWaterData)
                {
                    try
                    {
                        WaterUtils.SampleHeight(ref water, ref terrain, new float3(n.x, 0f, n.y), out float depth);
                        if (depth > 0.05f)
                            waterTop = math.max(waterTop, ground + math.min(depth, k_MaxWaterDepth));
                    }
                    catch
                    {
                        haveWaterData = false;
                    }
                }

                if (ground < waterTop)
                {
                    anyWet = true;
                    maxWaterTop = math.max(maxWaterTop, waterTop);
                }
                else
                {
                    drySum += ground;
                    dryCount++;
                }
            }

            float target;
            if (!anyWet)
            {
                target = allSum / m_TmpNodes.Count;
            }
            else
            {
                float floor = maxWaterTop + k_WaterFreeboard;
                target = math.max(dryCount > 0 ? drySum / dryCount : floor, floor);
            }

            float minH = m_TerrainSystem.positionOffset.y;
            float maxH = minH + m_TerrainSystem.heightScaleOffset.x;
            float clamped = math.isfinite(target) ? math.clamp(target, minH, maxH) : minH;

            // DEBUG while the CTD is open: one line per clearly changed target.
            if (m_TargetLogLines < k_TargetLogMax && math.abs(clamped - m_LastLoggedTarget) > 0.5f)
            {
                m_TargetLogLines++;
                m_LastLoggedTarget = clamped;
             }
            return clamped;
        }

        /// <summary>Adds a course end point as a node, once per 0.1 m cell (neighbouring courses share their ends).</summary>
        private void AddNodeOnce(float2 p)
        {
            if (m_TmpNodeKeys.Add((int2)math.round(p * 10f)))
                m_TmpNodes.Add(p);
        }

        /// <summary>Collects the stamp's sub-net road prefabs and their largest half width.</summary>
        private bool GetStampRoadPrefabs(Entity stampPrefabEntity, out HashSet<Entity> roadPrefabs, out float halfWidth)
        {
            roadPrefabs = new HashSet<Entity>();
            halfWidth = 0f;
            if (stampPrefabEntity == Entity.Null || !EntityManager.HasBuffer<Game.Prefabs.SubNet>(stampPrefabEntity))
                return false;

            DynamicBuffer<Game.Prefabs.SubNet> subNets = EntityManager.GetBuffer<Game.Prefabs.SubNet>(stampPrefabEntity, true);
            for (int i = 0; i < subNets.Length; i++)
            {
                Entity p = subNets[i].m_Prefab;
                if (p == Entity.Null || !roadPrefabs.Add(p))
                    continue;

                if (EntityManager.HasComponent<NetGeometryData>(p))
                    halfWidth = math.max(halfWidth, EntityManager.GetComponentData<NetGeometryData>(p).m_DefaultWidth * 0.5f);
            }

            if (halfWidth < 0.1f)
                halfWidth = 4f;
            return roadPrefabs.Count > 0;
        }

        /// <summary>Records the stamp's newly created Temp nodes and edges on the commit frame.</summary>
        private bool TryCaptureBatch(Entity stampPrefabEntity, out PendingBatch batch)
        {
            batch = null;

            if (!GetStampRoadPrefabs(stampPrefabEntity, out HashSet<Entity> roadPrefabs, out float halfWidth))
                return false;

            batch = new PendingBatch
            {
                HalfWidth = halfWidth,
                Elevation = MertBaseToolSystem.LastHandoffElevation,
            };

            float sumY = 0f;
            using (NativeArray<Entity> nodes = m_TempNodeQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity e in nodes)
                {
                    if (!IsNewAndOurs(e, roadPrefabs))
                        continue;

                    batch.Nodes.Add(e);
                    sumY += EntityManager.GetComponentData<GameNetNode>(e).m_Position.y;
                }
            }

            using (NativeArray<Entity> edges = m_TempEdgeQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity e in edges)
                {
                    if (IsNewAndOurs(e, roadPrefabs))
                        batch.Edges.Add(e);
                }
            }

            if (batch.Nodes.Count == 0 || batch.Edges.Count == 0)
                return false;

            batch.RoadY = sumY / batch.Nodes.Count;
            return true;
        }

        /// <summary>Returns true for a newly created (not modified or deleted) Temp entity of one of our road prefabs.</summary>
        private bool IsNewAndOurs(Entity e, HashSet<Entity> roadPrefabs)
        {
            if (!roadPrefabs.Contains(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab))
                return false;

            Temp temp = EntityManager.GetComponentData<Temp>(e);
            return (temp.m_Flags & TempFlags.Create) != 0
                && (temp.m_Flags & TempFlags.Delete) == 0
                && temp.m_Original == Entity.Null;
        }

        /// <summary>Advances a captured commit: flattens the network, then bakes the terrain. Returns true when done.</summary>
        private bool TryProcessPending(PendingBatch batch)
        {
            batch.Nodes.RemoveAll(e => !EntityManager.Exists(e) || EntityManager.HasComponent<Deleted>(e));
            batch.Edges.RemoveAll(e => !EntityManager.Exists(e) || EntityManager.HasComponent<Deleted>(e));

            if (batch.Nodes.Count == 0 && batch.Edges.Count == 0)
            {
                ReleaseOverlay(batch);
                return true;
            }

            if (batch.Applied)
            {
                if (++batch.FramesSinceApply < k_RefreshDelayFrames)
                    return false;

                TagUpdated(CollectAffected(batch));
                return true;
            }

            if (!batch.NetworkDone)
            {
                batch.WaitedFrames++;

                bool stillTemp = batch.Nodes.Exists(e => EntityManager.HasComponent<Temp>(e))
                              || batch.Edges.Exists(e => EntityManager.HasComponent<Temp>(e));

                if (stillTemp)
                {
                    if (batch.WaitedFrames < k_MaxWaitFrames)
                        return false;

                    ReleaseOverlay(batch);
                    return true;
                }

                FlattenNetwork(batch);
                batch.NetworkDone = true;

                if (!batch.UsesOverlay)
                {
                    FlattenTerrain(batch);
                    batch.Applied = true;
                    batch.FramesSinceApply = 0;
                }

                return false;
            }

            batch.BakeWait++;
            if (m_Overlay.Active && m_Overlay.FramesSinceInject < k_BakeSettleFrames && batch.BakeWait < k_BakeMaxWaitFrames)
                return false;

            if (m_Overlay.Active)
            {
                BakeOverlay(batch);
                ReleaseOverlay(batch);
                return true;
            }

            ReleaseOverlay(batch);
            FlattenTerrain(batch);
            batch.Applied = true;
            batch.FramesSinceApply = 0;
            return false;
        }

        /// <summary>Releases the frozen preview overlay held by a commit.</summary>
        private void ReleaseOverlay(PendingBatch batch)
        {
            if (!batch.UsesOverlay || batch.Baked)
                return;

            batch.Baked = true;
            m_Overlay.Clear();
            m_PreviewValid = false;
        }

        /// <summary>Returns our nodes and edges plus every edge connected to our nodes and their end nodes.</summary>
        private HashSet<Entity> CollectAffected(PendingBatch batch)
        {
            var set = new HashSet<Entity>(batch.Nodes);
            foreach (Entity e in batch.Edges)
                set.Add(e);

            foreach (Entity n in batch.Nodes)
            {
                if (!EntityManager.HasBuffer<GameNetConnectedEdge>(n))
                    continue;

                DynamicBuffer<GameNetConnectedEdge> connected = EntityManager.GetBuffer<GameNetConnectedEdge>(n, true);
                for (int i = 0; i < connected.Length; i++)
                    set.Add(connected[i].m_Edge);
            }

            var endNodes = new List<Entity>();
            foreach (Entity e in set)
            {
                if (EntityManager.Exists(e) && EntityManager.HasComponent<GameNetEdge>(e))
                {
                    GameNetEdge edge = EntityManager.GetComponentData<GameNetEdge>(e);
                    endNodes.Add(edge.m_Start);
                    endNodes.Add(edge.m_End);
                }
            }

            foreach (Entity n in endNodes)
                set.Add(n);

            return set;
        }

        /// <summary>Adds the Updated tag to every valid entity in the set.</summary>
        private void TagUpdated(HashSet<Entity> entities)
        {
            foreach (Entity e in entities)
            {
                if (e == Entity.Null || !EntityManager.Exists(e) || EntityManager.HasComponent<Deleted>(e))
                    continue;

                if (!EntityManager.HasComponent<Updated>(e))
                    EntityManager.AddComponent<Updated>(e);
            }
        }

        /// <summary>Moves our nodes and curves to the target height, adjusting foreign edge ends, then tags them Updated.</summary>
        private void FlattenNetwork(PendingBatch batch)
        {
            var ourNodes = new HashSet<Entity>(batch.Nodes);
            var ourEdges = new HashSet<Entity>(batch.Edges);
            bool onGround = math.abs(batch.Elevation) < 0.01f;

            foreach (Entity n in batch.Nodes)
            {
                GameNetNode node = EntityManager.GetComponentData<GameNetNode>(n);
                node.m_Position.y = batch.RoadY;
                EntityManager.SetComponentData(n, node);
            }

            foreach (Entity n in batch.Nodes)
            {
                if (!EntityManager.HasBuffer<GameNetConnectedEdge>(n))
                    continue;

                DynamicBuffer<GameNetConnectedEdge> connected = EntityManager.GetBuffer<GameNetConnectedEdge>(n, true);
                for (int i = 0; i < connected.Length; i++)
                {
                    Entity e = connected[i].m_Edge;
                    if (ourEdges.Contains(e) || !EntityManager.HasComponent<GameNetCurve>(e))
                        continue;

                    GameNetEdge edge = EntityManager.GetComponentData<GameNetEdge>(e);
                    GameNetCurve curve = EntityManager.GetComponentData<GameNetCurve>(e);

                    float dStart = edge.m_Start == n ? batch.RoadY - curve.m_Bezier.a.y : 0f;
                    float dEnd = edge.m_End == n ? batch.RoadY - curve.m_Bezier.d.y : 0f;
                    if (math.abs(dStart) < 0.001f && math.abs(dEnd) < 0.001f)
                        continue;

                    curve.m_Bezier.a.y += dStart;
                    curve.m_Bezier.b.y += math.lerp(dStart, dEnd, 1f / 3f);
                    curve.m_Bezier.c.y += math.lerp(dStart, dEnd, 2f / 3f);
                    curve.m_Bezier.d.y += dEnd;
                    curve.m_Length = MathUtils.Length(curve.m_Bezier);
                    EntityManager.SetComponentData(e, curve);
                }
            }

            foreach (Entity e in batch.Edges)
            {
                GameNetEdge edge = EntityManager.GetComponentData<GameNetEdge>(e);
                GameNetCurve curve = EntityManager.GetComponentData<GameNetCurve>(e);

                float ya = ourNodes.Contains(edge.m_Start) ? batch.RoadY : EntityManager.GetComponentData<GameNetNode>(edge.m_Start).m_Position.y;
                float yd = ourNodes.Contains(edge.m_End) ? batch.RoadY : EntityManager.GetComponentData<GameNetNode>(edge.m_End).m_Position.y;

                curve.m_Bezier.a.y = ya;
                curve.m_Bezier.b.y = math.lerp(ya, yd, 1f / 3f);
                curve.m_Bezier.c.y = math.lerp(ya, yd, 2f / 3f);
                curve.m_Bezier.d.y = yd;
                curve.m_Length = MathUtils.Length(curve.m_Bezier);
                EntityManager.SetComponentData(e, curve);
            }

            foreach (Entity n in batch.Nodes)
                FixElevation(n, onGround, batch.Elevation);
            foreach (Entity e in batch.Edges)
                FixElevation(e, onGround, batch.Elevation);

            TagUpdated(CollectAffected(batch));
        }

        /// <summary>Removes the Elevation component on ground level, otherwise sets it to the tool elevation.</summary>
        private void FixElevation(Entity e, bool onGround, float elevation)
        {
            if (!EntityManager.HasComponent<GameNetElevation>(e))
                return;

            if (onGround)
            {
                EntityManager.RemoveComponent<GameNetElevation>(e);
            }
            else
            {
                GameNetElevation el = EntityManager.GetComponentData<GameNetElevation>(e);
                el.m_Elevation = new float2(elevation);
                EntityManager.SetComponentData(e, el);
            }
        }

        /// <summary>Fallback without preview: levels the terrain with a silhouette mask and a slope-adaptive falloff.</summary>
        private void FlattenTerrain(PendingBatch batch)
        {
            var samples = new List<float2x2>(batch.Edges.Count * k_CurveSamples);
            float2 min = new(float.MaxValue);
            float2 max = new(float.MinValue);

            foreach (Entity e in batch.Edges)
            {
                Bezier4x3 b = EntityManager.GetComponentData<GameNetCurve>(e).m_Bezier;
                float2 prev = b.a.xz;
                for (int s = 1; s <= k_CurveSamples; s++)
                {
                    float2 p = MathUtils.Position(b, s / (float)k_CurveSamples).xz;
                    samples.Add(new float2x2(prev, p));
                    min = math.min(min, math.min(prev, p));
                    max = math.max(max, math.max(prev, p));
                    prev = p;
                }
            }

            if (samples.Count == 0)
                return;

            float bandRadius = batch.HalfWidth + k_FlatMargin;
            float pad = bandRadius + k_MaxFalloff + 2f;
            float2 center = (min + max) * 0.5f;
            float brushSize = math.cmax(max - min) + 2f * pad;
            float2 origin = center - brushSize * 0.5f;
            float groundY = batch.RoadY - batch.Elevation;

            Func<float2, float> sampleH0 = null;
            if (m_Base.Ensure(origin, origin + brushSize))
            {
                sampleH0 = m_Base.Sample;
            }
            else
            {
                TerrainHeightData heightData = m_TerrainSystem.GetHeightData(false);
                if (heightData.isCreated)
                    sampleH0 = q => TerrainUtils.SampleHeight(ref heightData, new float3(q.x, 0f, q.y));
            }

            float[] weight = BuildSilhouetteWeights(samples, origin, brushSize, bandRadius, sampleH0, groundY);

            int res = k_MaskResolution;
            var colors = new Color[res * res];
            for (int k = 0; k < colors.Length; k++)
            {
                float w = weight[k];
                colors[k] = new Color(w, w, w, w);
            }

            Texture2D mask = GetMaskTexture(res);
            mask.SetPixels(colors);
            mask.Apply(false, false);

            m_TerrainSystem.ApplyBrush(TerraformingType.Level, new Bounds2(origin, origin + brushSize), CreateBrush(center, groundY, brushSize), mask);
            m_Base.Invalidate();
        }

        /// <summary>Writes the heights shown by the preview overlay into the base heightmap with a single Level brush.</summary>
        private void BakeOverlay(PendingBatch batch)
        {
            List<float2> hull = batch.Hull;
            float groundY = batch.RoadY - batch.Elevation;

            FlattenFootprint.MinMax(hull, out float2 min, out float2 max);
            float limit = batch.MaxWidth + 2f;
            float pad = limit + 2f;
            float2 center = (min + max) * 0.5f;
            float brushSize = math.cmax(max - min) + 2f * pad;
            float2 origin = center - brushSize * 0.5f;

            // Always the full mask resolution: the mask texture is then created once and never destroyed while the
            // terrain may still use it for a brush applied earlier.
            int res = k_MaskResolution;
            float px = brushSize / res;

            TerrainHeightData cascade = m_TerrainSystem.GetHeightData(false);
            if (!cascade.isCreated || !m_Base.Ensure(origin, origin + brushSize))
            {
                FlattenTerrain(batch);
                return;
            }

            var colors = new Color[res * res];
            float[] outDist = OutsideDistanceField(hull, origin, px, res);

            for (int j = 0; j < res; j++)
            {
                for (int i = 0; i < res; i++)
                {
                    int k = j * res + i;
                    float dOut = outDist[k];
                    if (dOut > limit)
                        continue;

                    float2 wp = origin + (new float2(i, j) + 0.5f) * px;
                    float h0 = m_Base.Sample(wp);
                    float hp = TerrainUtils.SampleHeight(ref cascade, new float3(wp.x, 0f, wp.y));
                    float dh = groundY - h0;

                    float w = math.abs(dh) < 0.05f
                        ? (dOut <= 0f ? 1f : 0f)
                        : math.saturate((hp - h0) / dh);

                    colors[k] = new Color(w, w, w, w);
                }
            }

            Texture2D mask = GetMaskTexture(res);
            mask.SetPixels(colors);
            mask.Apply(false, false);

            m_TerrainSystem.ApplyBrush(TerraformingType.Level, new Bounds2(origin, origin + brushSize), CreateBrush(center, groundY, brushSize), mask);
            m_Base.ApplyLevel(origin, brushSize, res, colors, groundY);
        }

        /// <summary>Creates a world-aligned, full-strength Level brush centered at the given position.</summary>
        private static Brush CreateBrush(float2 center, float groundY, float size)
        {
            var worldCenter = new float3(center.x, groundY, center.y);
            return new Brush
            {
                m_Tool = Entity.Null,
                m_Position = worldCenter,
                m_Target = worldCenter,
                m_Start = worldCenter,
                m_Angle = 0f,
                m_Size = size,
                m_Strength = 1f,
                m_Opacity = 1f,
            };
        }

        /// <summary>Returns the distance (m) from each mask pixel to a convex hull, 0 inside (chamfer 3-4 approximation).</summary>
        private static float[] OutsideDistanceField(List<float2> hull, float2 origin, float px, int res)
        {
            const int INF = int.MaxValue / 4;
            var dist = new int[res * res];

            for (int j = 0; j < res; j++)
            {
                float z = origin.y + (j + 0.5f) * px;
                float xl = float.MaxValue, xr = float.MinValue;
                for (int e = 0; e < hull.Count; e++)
                {
                    float2 a = hull[e];
                    float2 b = hull[(e + 1) % hull.Count];
                    if ((z < a.y && z < b.y) || (z > a.y && z > b.y))
                        continue;
                    bool flat = math.abs(b.y - a.y) < 1e-5f;
                    float x = flat ? math.min(a.x, b.x) : math.lerp(a.x, b.x, (z - a.y) / (b.y - a.y));
                    float x2 = flat ? math.max(a.x, b.x) : x;
                    xl = math.min(xl, x);
                    xr = math.max(xr, x2);
                }

                for (int i = 0; i < res; i++)
                {
                    float x = origin.x + (i + 0.5f) * px;
                    dist[j * res + i] = (x >= xl && x <= xr) ? 0 : INF;
                }
            }

            ChamferPasses(dist, res);

            var result = new float[res * res];
            for (int k = 0; k < result.Length; k++)
                result[k] = dist[k] >= INF ? float.MaxValue : dist[k] / 3f * px;
            return result;
        }

        /// <summary>Runs the forward and backward chamfer (3-4) distance passes in place; 0 marks the seed pixels.</summary>
        private static void ChamferPasses(int[] dist, int res)
        {
            for (int j = 0; j < res; j++)
                for (int i = 0; i < res; i++)
                {
                    int idx = j * res + i, d = dist[idx];
                    if (d == 0) continue;
                    if (i > 0) d = math.min(d, dist[idx - 1] + 3);
                    if (j > 0)
                    {
                        d = math.min(d, dist[idx - res] + 3);
                        if (i > 0) d = math.min(d, dist[idx - res - 1] + 4);
                        if (i < res - 1) d = math.min(d, dist[idx - res + 1] + 4);
                    }
                    dist[idx] = d;
                }

            for (int j = res - 1; j >= 0; j--)
                for (int i = res - 1; i >= 0; i--)
                {
                    int idx = j * res + i, d = dist[idx];
                    if (d == 0) continue;
                    if (i < res - 1) d = math.min(d, dist[idx + 1] + 3);
                    if (j < res - 1)
                    {
                        d = math.min(d, dist[idx + res] + 3);
                        if (i < res - 1) d = math.min(d, dist[idx + res + 1] + 4);
                        if (i > 0) d = math.min(d, dist[idx + res - 1] + 4);
                    }
                    dist[idx] = d;
                }
        }

        /// <summary>Returns the reusable mask texture (every caller uses k_MaskResolution, so it is created once).</summary>
        private Texture2D GetMaskTexture(int res)
        {
            if (m_MaskTexture == null || m_MaskTexture.width != res)
            {
                if (m_MaskTexture != null)
                    UnityEngine.Object.Destroy(m_MaskTexture);

                m_MaskTexture = new Texture2D(res, res, TextureFormat.RGBAHalf, false, true)
                {
                    name = "MertsToolBox_FlattenMask",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
            }

            return m_MaskTexture;
        }

        /// <summary>
        /// Rasterizes the road bands, fills the enclosed interior and returns per-pixel Level weights:
        /// 1 inside, smoothstep falloff outside with a width derived from the local height difference.
        /// </summary>
        private float[] BuildSilhouetteWeights(List<float2x2> segments, float2 origin, float brushSize, float bandRadius,
            Func<float2, float> sampleH0, float groundY)
        {
            int res = k_MaskResolution;
            float px = brushSize / res;
            var solid = new bool[res * res];

            foreach (float2x2 seg in segments)
            {
                float2 a = seg.c0, b = seg.c1;
                int i0 = math.max(0, (int)math.floor((math.min(a.x, b.x) - bandRadius - origin.x) / px));
                int i1 = math.min(res - 1, (int)math.ceil((math.max(a.x, b.x) + bandRadius - origin.x) / px));
                int j0 = math.max(0, (int)math.floor((math.min(a.y, b.y) - bandRadius - origin.y) / px));
                int j1 = math.min(res - 1, (int)math.ceil((math.max(a.y, b.y) + bandRadius - origin.y) / px));

                float2 ab = b - a;
                float lenSq = math.max(math.lengthsq(ab), 1e-6f);
                for (int j = j0; j <= j1; j++)
                {
                    for (int i = i0; i <= i1; i++)
                    {
                        float2 p = origin + (new float2(i, j) + 0.5f) * px;
                        float t = math.saturate(math.dot(p - a, ab) / lenSq);
                        if (math.distancesq(p, a + ab * t) <= bandRadius * bandRadius)
                            solid[j * res + i] = true;
                    }
                }
            }

            var outside = new bool[res * res];
            var stack = new int[res * res];
            int stackCount = 0;
            for (int k = 0; k < res; k++)
            {
                PushIfOpen(k, 0); PushIfOpen(k, res - 1); PushIfOpen(0, k); PushIfOpen(res - 1, k);
            }
            while (stackCount > 0)
            {
                int idx = stack[--stackCount];
                int i = idx % res, j = idx / res;
                if (i > 0) PushIfOpen(i - 1, j);
                if (i < res - 1) PushIfOpen(i + 1, j);
                if (j > 0) PushIfOpen(i, j - 1);
                if (j < res - 1) PushIfOpen(i, j + 1);
            }

            void PushIfOpen(int i, int j)
            {
                int idx = j * res + i;
                if (solid[idx] || outside[idx]) return;
                outside[idx] = true;
                stack[stackCount++] = idx;
            }

            const int INF = int.MaxValue / 4;
            var dist = new int[res * res];
            for (int k = 0; k < dist.Length; k++)
                dist[k] = outside[k] ? INF : 0;

            ChamferPasses(dist, res);

            var weight = new float[res * res];
            for (int k = 0; k < weight.Length; k++)
            {
                if (dist[k] >= INF)
                    continue;

                if (dist[k] == 0)
                {
                    weight[k] = 1f;
                    continue;
                }

                float meters = dist[k] / 3f * px;
                float falloff = k_MaxFalloff;
                if (sampleH0 != null)
                {
                    int i = k % res, j = k / res;
                    float2 wp = origin + (new float2(i, j) + 0.5f) * px;
                    float dh = math.abs(sampleH0(wp) - groundY);
                    falloff = math.clamp(1.5f * dh / k_MaxSlope, k_MinFalloff, k_MaxFalloff);
                }

                weight[k] = 1f - math.smoothstep(0f, falloff, meters);
            }

            return weight;
        }
    }

    /// <summary>
    /// Runs the flatten preview late in the frame (register at SystemUpdatePhase.ModificationEnd): the object tool's
    /// definitions of this frame exist by then, and the overlay injected now is drawn in the same frame.
    /// </summary>
    public partial class MertFlattenLatePreviewSystem : SystemBase
    {
        /// <summary>Does nothing unless a flatten tool is active.</summary>
        protected override void OnUpdate()
        {
            if (!MertToolState.FlattenToolActive || !MertToolState.FlattenGeometryEnabled)
                return;

            MertToolBoxTerrainFlattenSystem.Instance?.LatePreviewUpdate();
        }
    }
}
