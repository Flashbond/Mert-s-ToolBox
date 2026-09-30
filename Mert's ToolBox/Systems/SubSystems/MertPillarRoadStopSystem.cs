using System.Collections.Generic;
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using MertsToolBox.Management;
using MertsToolBox.UI;
using MertsToolBox.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Transform = Game.Objects.Transform;

namespace MertsToolBox.Systems
{
    /// <summary>
    /// Keeps helix roads and piers valid without Anarchy:
    /// - road helix pillars end on the road (or the lower floor's beam) below them instead of running to the ground;
    /// - helix pier pillars (and those of piers touching a helix) are hidden in the preview; once built, the game's own
    ///   pillars are kept and only excluded from validation when a road they pass through is being edited;
    /// - committed helix edges are tagged (MertHelixRoad) so all of this still applies when vanilla tools edit them.
    /// Vanilla roads and piers that have nothing to do with a helix are never touched.
    /// </summary>
    public partial class MertPillarRoadStopSystem : SystemBase
    {
        private const float k_MinClearance = 1f;
        private const float k_WidthMargin = 0.5f;
        private const float k_StandingTolerance = 0.5f;
        private const float k_RevalidateRadius = 2.5f;
        private const float k_LegStackTolerance = 0.25f;
        private const float k_BeamContactLift = 0.05f;
        private const float k_CurveMatchTolerance = 0.5f;
        private const int k_TagGraceFrames = 30;
        private const int k_MaxPierChain = 64;

        private Game.Net.SearchSystem m_NetSearchSystem;
        private PrefabSystem m_PrefabSystem;
        private EntityQuery m_TempPillarQuery;
        private EntityQuery m_ChangedPillarQuery;
        private EntityQuery m_TempEdgeQuery;
        private EntityQuery m_CreatedEdgeQuery;
        private EntityQuery m_NewUntaggedEdgeQuery;
        private EntityQuery m_TaggedEdgeQuery;
        private EntityQuery m_ElevatedPermanentPillarQuery;
        private EntityQuery m_PermanentStandaloneQuery;
        private EntityQuery m_ErrorQuery;
        private EntityQuery m_LegIndexQuery;

        // Areas the current preview can affect (temp edge boxes, small squares around temp pillars), used to skip
        // built pillars that cannot be involved before any per-entity lookup is made.
        private readonly List<float4> m_CandidateRects = new();
        private float4 m_CandidateUnion;
        private readonly Dictionary<Entity, PillarType> m_PillarTypeCache = new();

        private bool m_HelixActive;
        private int m_FramesSinceHelix = int.MaxValue / 2;
        private readonly List<Bezier4x3> m_HelixCurves = new();
        private readonly List<float2> m_NewlyHidden = new();
        private readonly HashSet<Entity> m_Visited = new();
        private readonly List<Entity> m_Frontier = new();
        private NativeList<Entity> m_OverlayHits;

        // Leg/beam index for stacked double pillars, built at most once per frame and only when needed.
        private bool m_LegIndexBuilt;
        private readonly List<LegInfo> m_Legs = new();
        private readonly List<LegInfo> m_TempLegs = new();
        private readonly List<LegInfo> m_Beams = new();

        private struct LegInfo
        {
            public Entity Entity;
            public Entity Owner;
            public float3 Position;
            public float Top;
            public bool IsTemp;
        }

        private struct EdgeSample
        {
            public Entity Entity;
            public Bezier4x3 Curve;
            public float HalfWidth;
            public Bounds3 Bounds;
        }

        /// <summary>Resolves systems and builds the pillar and edge queries.</summary>
        protected override void OnCreate()
        {
            base.OnCreate();
            m_NetSearchSystem = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_OverlayHits = new NativeList<Entity>(16, Allocator.Persistent);

            m_TempPillarQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Pillar>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Transform>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                },
                None = new[] { ComponentType.ReadOnly<Deleted>() },
            });

            m_ChangedPillarQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Pillar>(),
                    ComponentType.ReadOnly<Transform>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<Created>(),
                    ComponentType.ReadOnly<Updated>(),
                },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() },
            });

            m_TempEdgeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Edge>(),
                    ComponentType.ReadOnly<Curve>(),
                    ComponentType.ReadOnly<Temp>(),
                },
                None = new[] { ComponentType.ReadOnly<Deleted>() },
            });

            m_CreatedEdgeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Edge>(),
                    ComponentType.ReadOnly<Curve>(),
                    ComponentType.ReadOnly<Created>(),
                },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() },
            });

            m_NewUntaggedEdgeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Edge>(), ComponentType.ReadOnly<Created>() },
                None = new[]
                {
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<MertHelixRoad>(),
                },
            });

            m_TaggedEdgeQuery = GetEntityQuery(ComponentType.ReadOnly<MertHelixRoad>(), ComponentType.Exclude<Deleted>());

            m_ElevatedPermanentPillarQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Pillar>(),
                    ComponentType.ReadOnly<Game.Objects.Elevation>(),
                    ComponentType.ReadOnly<Stack>(),
                    ComponentType.ReadOnly<Transform>(),
                },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() },
            });

            m_PermanentStandaloneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Pillar>(),
                    ComponentType.ReadOnly<Stack>(),
                    ComponentType.ReadOnly<Transform>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>() },
            });

            m_ErrorQuery = GetEntityQuery(ComponentType.ReadOnly<Game.Tools.Error>(), ComponentType.Exclude<Deleted>());

            m_LegIndexQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Pillar>(),
                    ComponentType.ReadOnly<Transform>(),
                    ComponentType.ReadOnly<Owner>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Game.Tools.Hidden>() },
            });
        }

        /// <summary>Releases the persistent search result buffer.</summary>
        protected override void OnDestroy()
        {
            if (m_OverlayHits.IsCreated)
                m_OverlayHits.Dispose();
            base.OnDestroy();
        }

        /// <summary>Tags committed helix edges, then handles the pillars of the current preview and of new builds.</summary>
        protected override void OnUpdate()
        {
            bool helix = MertToolState.HelixCleanupRequested;

            if (helix)
            {
                m_FramesSinceHelix = 0;
                SnapshotHelixCurves();
            }
            else if (m_FramesSinceHelix <= k_TagGraceFrames)
            {
                m_FramesSinceHelix++;
            }
            else if (m_HelixCurves.Count > 0)
            {
                m_HelixCurves.Clear();
            }

            if (m_HelixCurves.Count > 0 && !m_NewUntaggedEdgeQuery.IsEmptyIgnoreFilter)
                TagNewHelixEdges();

            // Helix built with the Anarchy option: Anarchy handles validation, our pillar fixes stand down.
            if (MertHelixBuildOption.AnarchyBuildActive)
            {
                MertPillarValidationWindow.Clear();
                return;
            }

            // With the helix tool closed there is only something to protect when the city contains a helix.
            if (!helix && m_TaggedEdgeQuery.IsEmptyIgnoreFilter)
            {
                MertPillarValidationWindow.Clear();
                return;
            }

            bool anyTempPillar = !m_TempPillarQuery.IsEmptyIgnoreFilter;
            bool anyChangedPillar = !m_ChangedPillarQuery.IsEmptyIgnoreFilter;
            bool anyTempEdge = !m_TempEdgeQuery.IsEmptyIgnoreFilter;
            if (!helix && !anyTempPillar && !anyChangedPillar && !anyTempEdge)
            {
                MertPillarValidationWindow.Clear();
                return;
            }

            m_HelixActive = helix;
            m_LegIndexBuilt = false;

            using NativeArray<Entity> tempPillars = m_TempPillarQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<Entity> changedPillars = m_ChangedPillarQuery.ToEntityArray(Allocator.Temp);
            if (tempPillars.Length == 0 && changedPillars.Length == 0 && !anyTempEdge)
                return;

            using NativeList<EdgeSample> tempEdges = BuildEdgeSamples(m_TempEdgeQuery);
            // Edges committed this frame are not in the net search tree yet; built pillars check them directly.
            using NativeList<EdgeSample> createdEdges = changedPillars.Length > 0
                ? BuildEdgeSamples(m_CreatedEdgeQuery)
                : new NativeList<EdgeSample>(0, Allocator.Temp);

            NativeQuadTree<Entity, QuadTreeBoundsXZ> tree = m_NetSearchSystem.GetNetSearchTree(true, out JobHandle dependencies);
            dependencies.Complete();

            var iterator = new RoadBelowIterator
            {
                m_Curve = GetComponentLookup<Curve>(true),
                m_Composition = GetComponentLookup<Composition>(true),
                m_CompositionData = GetComponentLookup<NetCompositionData>(true),
                m_Temp = GetComponentLookup<Temp>(true),
                m_Deleted = GetComponentLookup<Deleted>(true),
            };

            // The helix preview always needs the leg index; build it before any pillar is shortened.
            if (helix && tempPillars.Length > 0)
                EnsureLegIndex();

            foreach (Entity e in tempPillars)
                ProcessPillar(e, true, tempEdges, tree, ref iterator);

            TrackSiblingBeams(tempPillars);

            foreach (Entity e in changedPillars)
                ProcessPillar(e, false, createdEdges, tree, ref iterator);

            if (m_NewlyHidden.Count > 0 || !m_ErrorQuery.IsEmptyIgnoreFilter)
                RevalidateNeighbours(tempPillars);

            if (tempEdges.Length > 0)
            {
                BuildCandidateArea(tempEdges, tempPillars);
                TrackPermanentPillarsOnTempRoads(tempEdges);
                TrackPierPillarsThroughTempRoads(tempEdges);
            }
        }

        /// <summary>Collects every live, non-tunnel edge of the query (preview or just committed) with its half width and bounds.</summary>
        private NativeList<EdgeSample> BuildEdgeSamples(EntityQuery query)
        {
            var list = new NativeList<EdgeSample>(64, Allocator.Temp);
            using NativeArray<Entity> edges = query.ToEntityArray(Allocator.Temp);

            foreach (Entity edge in edges)
            {
                if (EntityManager.HasComponent<Temp>(edge)
                    && (EntityManager.GetComponentData<Temp>(edge).m_Flags & TempFlags.Delete) != 0)
                    continue;

                float halfWidth = 4f;
                if (EntityManager.HasComponent<Composition>(edge))
                {
                    Entity compositionEntity = EntityManager.GetComponentData<Composition>(edge).m_Edge;
                    if (EntityManager.HasComponent<NetCompositionData>(compositionEntity))
                    {
                        NetCompositionData data = EntityManager.GetComponentData<NetCompositionData>(compositionEntity);
                        if ((data.m_Flags.m_General & CompositionFlags.General.Tunnel) != 0)
                            continue;
                        halfWidth = data.m_Width * 0.5f;
                    }
                }

                Bezier4x3 curve = EntityManager.GetComponentData<Curve>(edge).m_Bezier;
                Bounds3 bounds = MathUtils.Bounds(curve);
                bounds.min.xz -= halfWidth + k_WidthMargin;
                bounds.max.xz += halfWidth + k_WidthMargin;

                list.Add(new EdgeSample { Entity = edge, Curve = curve, HalfWidth = halfWidth, Bounds = bounds });
            }

            return list;
        }

        /// <summary>Hides helix pier pillars in the preview, or ends a road pillar on the highest surface below it.</summary>
        private void ProcessPillar(Entity e, bool isTemp, NativeList<EdgeSample> edgeSamples,
            NativeQuadTree<Entity, QuadTreeBoundsXZ> tree, ref RoadBelowIterator iterator)
        {
            Entity prefab = EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
            if (!EntityManager.HasComponent<PillarData>(prefab)
                || !IsSupportedPillar(EntityManager.GetComponentData<PillarData>(prefab).m_Type)
                || !EntityManager.HasComponent<Stack>(e)
                || !EntityManager.HasComponent<StackData>(prefab)
                || !EntityManager.HasComponent<ObjectGeometryData>(prefab))
                return;

            // Helix pier pillars (and those of a pier touching a helix) are hidden in the preview: the preview only has
            // to validate cleanly, the game regenerates the pier's pillars itself once it is built.
            bool isStandalone = EntityManager.GetComponentData<PillarData>(prefab).m_Type == PillarType.Standalone;
            if (isTemp && isStandalone && (m_HelixActive || IsHelixRelated(e, Entity.Null) || PierTouchesHelix(e)))
            {
                HideStandalone(e, EntityManager.GetComponentData<Transform>(e).m_Position.xz);
                return;
            }

            // Built pier pillars keep the game's own shape (down to the ground); only their validation is handled
            // (TrackPierPillarsThroughTempRoads) when a road they pass through is being edited.
            if (!isTemp && isStandalone)
                return;

            Transform transform = EntityManager.GetComponentData<Transform>(e);
            Stack stack = EntityManager.GetComponentData<Stack>(e);
            float2 point = transform.m_Position.xz;
            float minY = transform.m_Position.y + stack.m_Range.min + 0.1f;
            float maxY = transform.m_Position.y + math.min(stack.m_Range.max, 0f) - k_MinClearance;
            Entity ownerEdge = EntityManager.HasComponent<Owner>(e) ? EntityManager.GetComponentData<Owner>(e).m_Owner : Entity.Null;

            if (maxY <= minY)
                return;

            bool found = false;
            float bestY = float.MinValue;
            Entity bestEdge = Entity.Null;

            // Preview edges, or edges committed this frame, are not in the net search tree yet: check them directly.
            for (int i = 0; i < edgeSamples.Length; i++)
            {
                EdgeSample s = edgeSamples[i];
                if (s.Entity == ownerEdge)
                    continue;
                if (point.x < s.Bounds.min.x || point.x > s.Bounds.max.x || point.y < s.Bounds.min.z || point.y > s.Bounds.max.z)
                    continue;

                float distance = MathUtils.Distance(s.Curve.xz, point, out float t);
                if (distance > s.HalfWidth + k_WidthMargin)
                    continue;

                float y = MathUtils.Position(s.Curve, t).y;
                if (y < minY || y > maxY || y <= bestY)
                    continue;

                bestY = y;
                bestEdge = s.Entity;
                found = true;
            }

            iterator.m_Point = point;
            iterator.m_MinY = minY;
            iterator.m_MaxY = maxY;
            iterator.m_SkipTemp = true;
            iterator.m_Found = false;
            iterator.m_BestY = float.MinValue;
            tree.Iterate(ref iterator);
            if (iterator.m_Found && iterator.m_BestY > bestY)
            {
                bestY = iterator.m_BestY;
                bestEdge = iterator.m_BestEntity;
                found = true;
            }

            // Only pillars of the helix itself (preview, or owned by a helix edge/node) or pillars standing on a helix
            // road are ever changed.
            bool ownerRelated = m_HelixActive || IsHelixRelated(e, Entity.Null);
            if (!ownerRelated && !(found && IsHelixRelated(e, bestEdge)))
                return;

            // Helix double pillars: the game widens a lower floor's beam so its outer leg reaches the entry tail's
            // sidewalk, and the floor above copies the same leg position. That upper leg then has no deck under it,
            // only the lower floor's widened beam; it stands on that beam instead of going down to the ground.
            if (TryFindLowerBeam(e, point, minY, maxY, out float beamTop) && beamTop > bestY)
            {
                // A foreign pillar above a helix road with a beam in between is left alone (shortening it onto the
                // road would run it through that beam).
                if (!ownerRelated)
                    return;
                bestY = beamTop;
                bestEdge = Entity.Null;
                found = true;
            }

            if (!found)
                return;

            if (isTemp && isStandalone)
            {
                HideStandalone(e, transform.m_Position.xz);
                return;
            }

            float groundY = transform.m_Position.y + stack.m_Range.min;
            ObjectGeometryData geometry = EntityManager.GetComponentData<ObjectGeometryData>(prefab);
            groundY -= geometry.m_Bounds.min.y;
            StackData stackData = EntityManager.GetComponentData<StackData>(prefab);

            stack.m_Range.min = bestY - transform.m_Position.y + geometry.m_Bounds.min.y;
            BatchDataHelpers.AlignStack(ref stack, stackData, false, true);
            EntityManager.SetComponentData(e, stack);

            var elevation = new Game.Objects.Elevation { m_Elevation = math.max(bestY - groundY, 0.01f), m_Flags = (Game.Objects.ElevationFlags)0 };
            if (EntityManager.HasComponent<Game.Objects.Elevation>(e))
                EntityManager.SetComponentData(e, elevation);
            else
                EntityManager.AddComponentData(e, elevation);

            if (isTemp)
                MertPillarValidationWindow.Track(e, bestEdge);

            if (!EntityManager.HasComponent<BatchesUpdated>(e))
                EntityManager.AddComponent<BatchesUpdated>(e);
        }

        /// <summary>
        /// Drops a preview pier pillar the way the game drops cancelled optional temps: ValidationSystem.Cancel marks
        /// them Hidden | Cancel (ApplyObjectsSystem then never creates them), and SubObjectHiddenSystem hides objects
        /// with the Hidden component + BatchesUpdated.
        /// </summary>
        private void HideStandalone(Entity e, float2 point)
        {
            Temp temp = EntityManager.GetComponentData<Temp>(e);
            const TempFlags wanted = TempFlags.Optional | TempFlags.Hidden | TempFlags.Cancel;
            if ((temp.m_Flags & wanted) != wanted)
            {
                temp.m_Flags |= wanted;
                EntityManager.SetComponentData(e, temp);
            }

            if (!EntityManager.HasComponent<Game.Tools.Hidden>(e))
            {
                EntityManager.AddComponent<Game.Tools.Hidden>(e);
                m_NewlyHidden.Add(point);
            }

            if (!EntityManager.HasComponent<BatchesUpdated>(e))
                EntityManager.AddComponent<BatchesUpdated>(e);
        }

        /// <summary>
        /// Collects every live, visible vertical leg (world top) and horizontal beam - temp and permanent - keyed by
        /// their owner, where a temp owner is replaced by the permanent owner it stands in for. Built once per frame.
        /// </summary>
        private void EnsureLegIndex()
        {
            if (m_LegIndexBuilt)
                return;
            m_LegIndexBuilt = true;
            m_Legs.Clear();
            m_TempLegs.Clear();
            m_Beams.Clear();

            // Positions, owners and prefabs are copied in bulk; only verticals and beams need further lookups.
            using NativeArray<Entity> pillars = m_LegIndexQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<Transform> transforms = m_LegIndexQuery.ToComponentDataArray<Transform>(Allocator.Temp);
            using NativeArray<Owner> owners = m_LegIndexQuery.ToComponentDataArray<Owner>(Allocator.Temp);
            using NativeArray<PrefabRef> prefabs = m_LegIndexQuery.ToComponentDataArray<PrefabRef>(Allocator.Temp);
            for (int i = 0; i < pillars.Length; i++)
            {
                Entity prefab = prefabs[i].m_Prefab;
                if (!TryGetPillarType(prefab, out PillarType type)
                    || (type != PillarType.Vertical && type != PillarType.Horizontal))
                    continue;

                Entity p = pillars[i];
                float3 pos = transforms[i].m_Position;
                bool isTemp = EntityManager.HasComponent<Temp>(p);
                Entity owner = owners[i].m_Owner;
                Entity ownerOriginal = isTemp ? OriginalOf(owner) : Entity.Null;
                var info = new LegInfo
                {
                    Entity = p,
                    Owner = ownerOriginal != Entity.Null ? ownerOriginal : owner,
                    Position = pos,
                    Top = pos.y,
                    IsTemp = isTemp,
                };

                if (type == PillarType.Vertical && EntityManager.HasComponent<Stack>(p))
                {
                    info.Top = pos.y + math.min(EntityManager.GetComponentData<Stack>(p).m_Range.max, 0f);
                    m_Legs.Add(info);
                    if (info.IsTemp)
                        m_TempLegs.Add(info);
                }
                else if (type == PillarType.Horizontal && EntityManager.HasComponent<ObjectGeometryData>(prefab))
                {
                    info.Top = pos.y + EntityManager.GetComponentData<ObjectGeometryData>(prefab).m_Bounds.max.y;
                    m_Beams.Add(info);
                }
            }
        }

        /// <summary>Pillar type of a prefab, cached (there are only a handful of pillar prefabs); false when it is no pillar.</summary>
        private bool TryGetPillarType(Entity prefab, out PillarType type)
        {
            if (m_PillarTypeCache.TryGetValue(prefab, out type))
                return true;
            if (!EntityManager.HasComponent<PillarData>(prefab))
                return false;
            type = EntityManager.GetComponentData<PillarData>(prefab).m_Type;
            m_PillarTypeCache[prefab] = type;
            return true;
        }

        /// <summary>
        /// Looks for another vertical leg (temp or permanent) exactly below this point (a lower floor's leg at the same
        /// XZ). The surface to stand on is the top of that leg's horizontal beam (same owner), or the leg top when no
        /// beam belongs to it. Returns the highest such surface inside [minY, maxY].
        /// </summary>
        private bool TryFindLowerBeam(Entity self, float2 point, float minY, float maxY, out float surfaceY)
        {
            EnsureLegIndex();
            surfaceY = float.MinValue;
            bool found = false;
            foreach (LegInfo leg in m_Legs)
            {
                if (leg.Entity == self || math.distance(leg.Position.xz, point) > k_LegStackTolerance)
                    continue;

                float y = BeamTopOver(leg, point) + k_BeamContactLift;
                if (y < minY || y > maxY || y <= surfaceY)
                    continue;
                surfaceY = y;
                found = true;
            }
            return found;
        }

        /// <summary>Top of the beam that sits on this leg (same owner and temp state, nearest in XZ); the leg top when it carries none.</summary>
        private float BeamTopOver(LegInfo leg, float2 point)
        {
            float y = leg.Top;
            float bestBeamDist = float.MaxValue;
            foreach (LegInfo beam in m_Beams)
            {
                if (beam.Owner != leg.Owner || beam.IsTemp != leg.IsTemp)
                    continue;
                float d = math.distance(beam.Position.xz, point);
                if (d < bestBeamDist)
                {
                    bestBeamDist = d;
                    y = beam.Top;
                }
            }
            return y;
        }

        /// <summary>
        /// True when a permanent pillar's bottom rests on the beam of a lower leg that is currently a temp copy
        /// (being rebuilt by an edit of the road below), so that rebuilt beam does not report the pillar as an overlap.
        /// </summary>
        private bool IsOnTempBeam(Entity pillar, float2 point, float bottom)
        {
            EnsureLegIndex();
            foreach (LegInfo leg in m_TempLegs)
            {
                if (leg.Entity == pillar || math.distance(leg.Position.xz, point) > k_LegStackTolerance)
                    continue;
                if (math.abs(BeamTopOver(leg, point) + k_BeamContactLift - bottom) <= k_StandingTolerance)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Remembers the curves of the helix preview's own edges so the committed copies can be recognised. Temp copies
        /// of existing edges and new pieces that run along an existing road (the road being split where the helix
        /// connects to it) are left out, so the connected road is never tagged.
        /// </summary>
        private void SnapshotHelixCurves()
        {
            if (m_TempEdgeQuery.IsEmptyIgnoreFilter)
                return;

            Entity stampPrefab = ResolveStampPrefab();
            NativeQuadTree<Entity, QuadTreeBoundsXZ> tree = m_NetSearchSystem.GetNetSearchTree(true, out JobHandle treeDeps);
            treeDeps.Complete();

            using NativeArray<Entity> edges = m_TempEdgeQuery.ToEntityArray(Allocator.Temp);
            m_HelixCurves.Clear();
            foreach (Entity edge in edges)
            {
                Temp temp = EntityManager.GetComponentData<Temp>(edge);
                if ((temp.m_Flags & TempFlags.Delete) != 0)
                    continue;

                bool take;
                if (stampPrefab != Entity.Null && IsOwnedByStamp(edge, stampPrefab))
                    take = true;
                else if (temp.m_Original != Entity.Null || (temp.m_Flags & TempFlags.Create) == 0)
                    take = false;
                else
                    take = !LiesOnExistingEdge(EntityManager.GetComponentData<Curve>(edge).m_Bezier, tree);

                if (take)
                    m_HelixCurves.Add(EntityManager.GetComponentData<Curve>(edge).m_Bezier);
            }
        }

        /// <summary>
        /// True when the curve runs along a permanent edge over its whole length (sampled at 1/4, 1/2 and 3/4):
        /// the new pieces of a road that is being split where the helix connects to it.
        /// </summary>
        private bool LiesOnExistingEdge(Bezier4x3 curve, NativeQuadTree<Entity, QuadTreeBoundsXZ> tree)
        {
            for (int k = 1; k <= 3; k++)
            {
                float3 p = MathUtils.Position(curve, k * 0.25f);
                m_OverlayHits.Clear();
                var hits = new EdgePointIterator { m_Point = p.xz, m_Tolerance = 0.5f, m_Results = m_OverlayHits };
                tree.Iterate(ref hits);

                bool onEdge = false;
                for (int i = 0; i < m_OverlayHits.Length && !onEdge; i++)
                {
                    Entity other = m_OverlayHits[i];
                    if (!EntityManager.HasComponent<Edge>(other) || !EntityManager.HasComponent<Curve>(other)
                        || EntityManager.HasComponent<Temp>(other) || EntityManager.HasComponent<Deleted>(other))
                        continue;
                    Bezier4x3 c = EntityManager.GetComponentData<Curve>(other).m_Bezier;
                    float d = MathUtils.Distance(c.xz, p.xz, out float t);
                    onEdge = d < 0.5f && math.abs(MathUtils.Position(c, t).y - p.y) < 1.5f;
                }
                if (!onEdge)
                    return false;
            }
            return true;
        }

        /// <summary>Prefab entity of the shared runtime stamp the helix tool hands to the object tool, or Entity.Null.</summary>
        private Entity ResolveStampPrefab()
        {
            AssetStampPrefab stamp = MertsToolBox.MertBaseToolSystem.SharedRuntimeStamp;
            if (stamp == null)
                return Entity.Null;
            return m_PrefabSystem.TryGetEntity(stamp, out Entity entity) && EntityManager.Exists(entity) ? entity : Entity.Null;
        }

        /// <summary>True when the entity's owner chain leads to a placed instance of the runtime stamp.</summary>
        private bool IsOwnedByStamp(Entity entity, Entity stampPrefab)
        {
            Entity current = entity;
            for (int depth = 0; depth < 4 && EntityManager.HasComponent<Owner>(current); depth++)
            {
                current = EntityManager.GetComponentData<Owner>(current).m_Owner;
                if (current == Entity.Null || !EntityManager.Exists(current))
                    return false;
                if (EntityManager.HasComponent<PrefabRef>(current)
                    && EntityManager.GetComponentData<PrefabRef>(current).m_Prefab == stampPrefab)
                    return true;
            }
            return false;
        }

        /// <summary>Tags newly created permanent edges whose curve matches one of the remembered helix preview curves.</summary>
        private void TagNewHelixEdges()
        {
            using NativeArray<Entity> created = m_NewUntaggedEdgeQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity edge in created)
            {
                if (!EntityManager.HasComponent<Curve>(edge))
                    continue;

                Bezier4x3 curve = EntityManager.GetComponentData<Curve>(edge).m_Bezier;
                foreach (Bezier4x3 h in m_HelixCurves)
                {
                    bool forward = math.distance(curve.a, h.a) < k_CurveMatchTolerance && math.distance(curve.d, h.d) < k_CurveMatchTolerance;
                    bool backward = math.distance(curve.a, h.d) < k_CurveMatchTolerance && math.distance(curve.d, h.a) < k_CurveMatchTolerance;
                    if ((forward || backward)
                        && math.distance(MathUtils.Position(curve, 0.5f), MathUtils.Position(h, 0.5f)) < k_CurveMatchTolerance)
                    {
                        EntityManager.AddComponent<MertHelixRoad>(edge);
                        break;
                    }
                }
            }
        }

        /// <summary>Returns true when the road below or the pillar's own owner (edge, or node with a tagged edge) is a helix road.</summary>
        private bool IsHelixRelated(Entity pillar, Entity road)
        {
            if (IsTagged(road))
                return true;
            return EntityManager.HasComponent<Owner>(pillar) && IsTagged(EntityManager.GetComponentData<Owner>(pillar).m_Owner);
        }

        /// <summary>
        /// Checks the helix tag on an entity or on the permanent entity a temp copy stands in for. Pillars of double
        /// pillar roads are owned by nodes, which carry no tag, so a node counts when one of its edges is tagged.
        /// </summary>
        private bool IsTagged(Entity entity)
        {
            if (HasHelixTag(entity))
                return true;
            if (entity == Entity.Null || !EntityManager.Exists(entity))
                return false;
            return NodeHasTaggedEdge(entity) || NodeHasTaggedEdge(OriginalOf(entity));
        }

        /// <summary>The tag on the entity itself or on its permanent original.</summary>
        private bool HasHelixTag(Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
                return false;
            if (EntityManager.HasComponent<MertHelixRoad>(entity))
                return true;
            Entity original = OriginalOf(entity);
            return original != Entity.Null && EntityManager.Exists(original) && EntityManager.HasComponent<MertHelixRoad>(original);
        }

        /// <summary>True when the entity is a node with at least one tagged connected edge.</summary>
        private bool NodeHasTaggedEdge(Entity node)
        {
            if (node == Entity.Null || !EntityManager.Exists(node) || !EntityManager.HasBuffer<ConnectedEdge>(node))
                return false;
            DynamicBuffer<ConnectedEdge> connected = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            for (int i = 0; i < connected.Length; i++)
            {
                if (HasHelixTag(connected[i].m_Edge))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True when the pier the pillar belongs to is connected to a helix: its owner node, or any node of the
        /// chain of same-prefab pier edges it belongs to (preview or built), connects to a tagged helix edge.
        /// </summary>
        private bool PierTouchesHelix(Entity pillar)
        {
            if (!EntityManager.HasComponent<Owner>(pillar))
                return false;
            Entity owner = EntityManager.GetComponentData<Owner>(pillar).m_Owner;
            if (owner == Entity.Null || !EntityManager.Exists(owner))
                return false;
            if (!EntityManager.HasComponent<Edge>(owner))
                return NodeHasTaggedEdge(owner) || NodeHasTaggedEdge(OriginalOf(owner));
            if (!EntityManager.HasComponent<PrefabRef>(owner))
                return false;

            Entity pierPrefab = EntityManager.GetComponentData<PrefabRef>(owner).m_Prefab;
            m_Visited.Clear();
            m_Frontier.Clear();
            m_Frontier.Add(owner);
            int head = 0;
            while (head < m_Frontier.Count && m_Visited.Count < k_MaxPierChain)
            {
                Entity edge = m_Frontier[head++];
                if (!m_Visited.Add(edge) || !EntityManager.Exists(edge) || !EntityManager.HasComponent<Edge>(edge)
                    || EntityManager.HasComponent<Deleted>(edge) || !EntityManager.HasComponent<PrefabRef>(edge)
                    || EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab != pierPrefab)
                    continue;
                if (EntityManager.HasComponent<Temp>(edge) && (EntityManager.GetComponentData<Temp>(edge).m_Flags & TempFlags.Delete) != 0)
                    continue;

                Edge e = EntityManager.GetComponentData<Edge>(edge);
                if (NodeHasTaggedEdge(e.m_Start) || NodeHasTaggedEdge(e.m_End)
                    || NodeHasTaggedEdge(OriginalOf(e.m_Start)) || NodeHasTaggedEdge(OriginalOf(e.m_End)))
                    return true;

                AddConnectedEdges(e.m_Start);
                AddConnectedEdges(e.m_End);
            }
            return false;
        }

        /// <summary>Queues every edge connected to a node.</summary>
        private void AddConnectedEdges(Entity node)
        {
            if (node == Entity.Null || !EntityManager.Exists(node) || !EntityManager.HasBuffer<ConnectedEdge>(node))
                return;
            DynamicBuffer<ConnectedEdge> connected = EntityManager.GetBuffer<ConnectedEdge>(node, true);
            for (int i = 0; i < connected.Length; i++)
                m_Frontier.Add(connected[i].m_Edge);
        }

        /// <summary>Returns the permanent entity a temp entity stands in for, or Entity.Null.</summary>
        private Entity OriginalOf(Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity) || !EntityManager.HasComponent<Temp>(entity))
                return Entity.Null;
            return EntityManager.GetComponentData<Temp>(entity).m_Original;
        }

        /// <summary>
        /// Collects the areas the current preview can affect: every temp edge's box (already widened by the road's
        /// half width) and a small square around every temp pillar (for built pillars standing on a rebuilt beam).
        /// </summary>
        private void BuildCandidateArea(NativeList<EdgeSample> tempEdges, NativeArray<Entity> tempPillars)
        {
            m_CandidateRects.Clear();
            m_CandidateUnion = new float4(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

            for (int i = 0; i < tempEdges.Length; i++)
            {
                Bounds3 b = tempEdges[i].Bounds;
                AddCandidateRect(new float4(b.min.x, b.min.z, b.max.x, b.max.z));
            }

            foreach (Entity p in tempPillars)
            {
                float2 q = EntityManager.GetComponentData<Transform>(p).m_Position.xz;
                AddCandidateRect(new float4(q.x - k_LegStackTolerance, q.y - k_LegStackTolerance, q.x + k_LegStackTolerance, q.y + k_LegStackTolerance));
            }
        }

        /// <summary>Adds one (minX, minZ, maxX, maxZ) rectangle to the candidate area and grows the union.</summary>
        private void AddCandidateRect(float4 rect)
        {
            m_CandidateRects.Add(rect);
            m_CandidateUnion = new float4(math.min(m_CandidateUnion.xy, rect.xy), math.max(m_CandidateUnion.zw, rect.zw));
        }

        /// <summary>
        /// True when the point lies inside one of the candidate rectangles. Only float comparisons: pillars outside
        /// cannot pass any of the checks that follow, so skipping them changes nothing.
        /// </summary>
        private bool InCandidateArea(float2 p)
        {
            if (p.x < m_CandidateUnion.x || p.y < m_CandidateUnion.y || p.x > m_CandidateUnion.z || p.y > m_CandidateUnion.w)
                return false;
            foreach (float4 r in m_CandidateRects)
            {
                if (p.x >= r.x && p.y >= r.y && p.x <= r.z && p.y <= r.w)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Adds already-built, shortened helix pillars to this frame's validation window when the road or the beam they
        /// stand on is being edited (temp), so the edited road or rebuilt beam does not report them as overlaps.
        /// </summary>
        private void TrackPermanentPillarsOnTempRoads(NativeList<EdgeSample> tempEdges)
        {
            using NativeArray<Entity> pillars = m_ElevatedPermanentPillarQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<Transform> transforms = m_ElevatedPermanentPillarQuery.ToComponentDataArray<Transform>(Allocator.Temp);
            for (int n = 0; n < pillars.Length; n++)
            {
                float3 position = transforms[n].m_Position;
                float2 point = position.xz;
                if (!InCandidateArea(point))
                    continue;

                Entity pillar = pillars[n];
                float bottom = position.y + EntityManager.GetComponentData<Stack>(pillar).m_Range.min;
                bool tracked = false;

                // Where the pillar actually touches the surface: the stack bottom minus the mesh's own lower bound.
                float contact = bottom;
                if (EntityManager.HasComponent<PrefabRef>(pillar))
                {
                    Entity pillarPrefab = EntityManager.GetComponentData<PrefabRef>(pillar).m_Prefab;
                    if (EntityManager.HasComponent<ObjectGeometryData>(pillarPrefab))
                        contact = bottom - EntityManager.GetComponentData<ObjectGeometryData>(pillarPrefab).m_Bounds.min.y;
                }

                for (int i = 0; i < tempEdges.Length; i++)
                {
                    EdgeSample s = tempEdges[i];
                    if (point.x < s.Bounds.min.x || point.x > s.Bounds.max.x || point.y < s.Bounds.min.z || point.y > s.Bounds.max.z)
                        continue;

                    float distance = MathUtils.Distance(s.Curve.xz, point, out float t);
                    if (distance > s.HalfWidth + k_WidthMargin)
                        continue;

                    float roadY = MathUtils.Position(s.Curve, t).y;
                    if (math.abs(roadY - bottom) > k_StandingTolerance && math.abs(roadY - contact) > k_StandingTolerance)
                        continue;

                    // Vanilla pillars keep vanilla validation: only helix pillars, or pillars on a helix road.
                    if (!IsHelixRelated(pillar, s.Entity))
                        continue;

                    MertPillarValidationWindow.TrackForFrame(pillar);
                    tracked = true;
                    break;
                }

                if (!tracked && IsOnTempBeam(pillar, point, bottom) && IsHelixRelated(pillar, Entity.Null))
                    MertPillarValidationWindow.TrackForFrame(pillar);
            }
        }

        /// <summary>
        /// Built pier pillars of a helix, or of a pier that touches a helix, run down to the ground and may pass
        /// through a road (the helix entry tail, a lower turn, a road median). When that road is being edited, the
        /// pillar joins this frame's validation window so the edited road does not report it as an overlap.
        /// </summary>
        private void TrackPierPillarsThroughTempRoads(NativeList<EdgeSample> tempEdges)
        {
            using NativeArray<Entity> pillars = m_PermanentStandaloneQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<Transform> transforms = m_PermanentStandaloneQuery.ToComponentDataArray<Transform>(Allocator.Temp);
            using NativeArray<PrefabRef> prefabs = m_PermanentStandaloneQuery.ToComponentDataArray<PrefabRef>(Allocator.Temp);
            for (int n = 0; n < pillars.Length; n++)
            {
                float3 position = transforms[n].m_Position;
                float2 point = position.xz;
                if (!InCandidateArea(point))
                    continue;
                if (!TryGetPillarType(prefabs[n].m_Prefab, out PillarType type) || type != PillarType.Standalone)
                    continue;

                Entity pillar = pillars[n];
                Stack stack = EntityManager.GetComponentData<Stack>(pillar);
                float bottom = position.y + stack.m_Range.min;
                float top = position.y + stack.m_Range.max;

                bool crosses = false;
                for (int i = 0; i < tempEdges.Length && !crosses; i++)
                {
                    EdgeSample s = tempEdges[i];
                    if (point.x < s.Bounds.min.x || point.x > s.Bounds.max.x || point.y < s.Bounds.min.z || point.y > s.Bounds.max.z)
                        continue;
                    float distance = MathUtils.Distance(s.Curve.xz, point, out float t);
                    if (distance > s.HalfWidth + k_WidthMargin)
                        continue;
                    float roadY = MathUtils.Position(s.Curve, t).y;
                    crosses = roadY >= bottom - k_StandingTolerance && roadY <= top + k_StandingTolerance;
                }

                if (!crosses || MertPillarValidationWindow.FrameOnly.Contains(pillar))
                    continue;
                if (!IsHelixRelated(pillar, Entity.Null) && !PierTouchesHelix(pillar))
                    continue;

                MertPillarValidationWindow.TrackForFrame(pillar);
            }
        }

        /// <summary>Pillar types that stand on the ground as a stretched stack: road double pillars and single pier/path pillars.</summary>
        private static bool IsSupportedPillar(PillarType type)
        {
            return type == PillarType.Vertical || type == PillarType.Standalone;
        }

        /// <summary>
        /// A double pillar's horizontal beam sits on top of the two vertical pillars. When one of those verticals was
        /// shortened onto a road below, the beam hangs inside that road's clearance envelope, so it joins the same
        /// validation window as its sibling verticals (same owner).
        /// </summary>
        private void TrackSiblingBeams(NativeArray<Entity> tempPillars)
        {
            if (MertPillarValidationWindow.Tracked.Count == 0)
                return;

            var owners = new HashSet<Entity>();
            foreach (Entity tracked in MertPillarValidationWindow.Tracked.Keys)
            {
                if (EntityManager.Exists(tracked) && EntityManager.HasComponent<Owner>(tracked))
                    owners.Add(EntityManager.GetComponentData<Owner>(tracked).m_Owner);
            }
            if (owners.Count == 0)
                return;

            foreach (Entity p in tempPillars)
            {
                if (MertPillarValidationWindow.Tracked.ContainsKey(p) || !EntityManager.HasComponent<Owner>(p))
                    continue;
                if (!owners.Contains(EntityManager.GetComponentData<Owner>(p).m_Owner))
                    continue;

                Entity prefab = EntityManager.GetComponentData<PrefabRef>(p).m_Prefab;
                if (!EntityManager.HasComponent<PillarData>(prefab)
                    || EntityManager.GetComponentData<PillarData>(prefab).m_Type != PillarType.Horizontal)
                    continue;

                MertPillarValidationWindow.Track(p, Entity.Null);
            }
        }

        /// <summary>
        /// Validation only runs on temps marked Updated and caches its verdict as Error components. When a pillar is
        /// hidden after its neighbours were already validated, their stale errors would stay until the next drag.
        /// Marking the visible temp pillars next to a newly hidden one as Updated makes this frame's validation
        /// re-check them against the (now ignored) hidden pillar.
        /// </summary>
        private void RevalidateNeighbours(NativeArray<Entity> tempPillars)
        {
            foreach (Entity p in tempPillars)
            {
                if (!EntityManager.Exists(p) || EntityManager.HasComponent<Game.Tools.Hidden>(p)
                    || EntityManager.HasComponent<Deleted>(p) || EntityManager.HasComponent<Updated>(p))
                    continue;

                bool stale = EntityManager.HasComponent<Game.Tools.Error>(p);
                if (!stale && m_NewlyHidden.Count == 0)
                    continue;

                float2 q = EntityManager.GetComponentData<Transform>(p).m_Position.xz;
                if (stale && HasHiddenPillarNear(tempPillars, p, q))
                {
                    EntityManager.AddComponent<Updated>(p);
                    continue;
                }

                foreach (float2 h in m_NewlyHidden)
                {
                    if (math.distance(q, h) > k_RevalidateRadius)
                        continue;

                    EntityManager.AddComponent<Updated>(p);
                    break;
                }
            }
            m_NewlyHidden.Clear();
        }

        /// <summary>Returns true when a hidden temp pillar stands within the revalidation radius of the given point.</summary>
        private bool HasHiddenPillarNear(NativeArray<Entity> tempPillars, Entity self, float2 point)
        {
            foreach (Entity o in tempPillars)
            {
                if (o == self || !EntityManager.HasComponent<Game.Tools.Hidden>(o))
                    continue;
                if (math.distance(EntityManager.GetComponentData<Transform>(o).m_Position.xz, point) <= k_RevalidateRadius)
                    return true;
            }
            return false;
        }

        /// <summary>Collects every search tree item whose XZ bounds come within the tolerance of a point.</summary>
        private struct EdgePointIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public float2 m_Point;
            public float m_Tolerance;
            public NativeList<Entity> m_Results;

            /// <summary>Accepts tree nodes whose XZ bounds contain the point (expanded by the tolerance).</summary>
            public readonly bool Intersect(QuadTreeBoundsXZ bounds)
            {
                Bounds3 b = bounds.m_Bounds;
                return m_Point.x >= b.min.x - m_Tolerance && m_Point.x <= b.max.x + m_Tolerance
                    && m_Point.y >= b.min.z - m_Tolerance && m_Point.y <= b.max.z + m_Tolerance;
            }

            /// <summary>Adds every item whose bounds pass the XZ test.</summary>
            public void Iterate(QuadTreeBoundsXZ bounds, Entity item)
            {
                if (Intersect(bounds))
                    m_Results.Add(item);
            }
        }

        private struct RoadBelowIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public float2 m_Point;
            public float m_MinY;
            public float m_MaxY;
            public bool m_SkipTemp;
            public bool m_Found;
            public float m_BestY;
            public Entity m_BestEntity;

            public ComponentLookup<Curve> m_Curve;
            public ComponentLookup<Composition> m_Composition;
            public ComponentLookup<NetCompositionData> m_CompositionData;
            public ComponentLookup<Temp> m_Temp;
            public ComponentLookup<Deleted> m_Deleted;

            /// <summary>Accepts tree nodes whose XZ bounds contain the pillar point and overlap the pillar's height span.</summary>
            public readonly bool Intersect(QuadTreeBoundsXZ bounds)
            {
                Bounds3 b = bounds.m_Bounds;
                return m_Point.x >= b.min.x && m_Point.x <= b.max.x
                    && m_Point.y >= b.min.z && m_Point.y <= b.max.z
                    && b.min.y <= m_MaxY && b.max.y >= m_MinY;
            }

            /// <summary>Keeps the highest permanent, non-tunnel road surface under the pillar point within the height span.</summary>
            public void Iterate(QuadTreeBoundsXZ bounds, Entity item)
            {
                if (!Intersect(bounds))
                    return;
                if (m_SkipTemp && m_Temp.HasComponent(item))
                    return;
                if (m_Deleted.HasComponent(item))
                    return;
                if (!m_Curve.TryGetComponent(item, out Curve curve))
                    return;
                if (!m_Composition.TryGetComponent(item, out Composition composition)
                    || !m_CompositionData.TryGetComponent(composition.m_Edge, out NetCompositionData data))
                    return;
                if ((data.m_Flags.m_General & CompositionFlags.General.Tunnel) != 0)
                    return;

                float distance = MathUtils.Distance(curve.m_Bezier.xz, m_Point, out float t);
                if (distance > data.m_Width * 0.5f + k_WidthMargin)
                    return;

                float y = MathUtils.Position(curve.m_Bezier, t).y;
                if (y < m_MinY || y > m_MaxY || y <= m_BestY)
                    return;

                m_BestY = y;
                m_BestEntity = item;
                m_Found = true;
            }
        }
    }
}
