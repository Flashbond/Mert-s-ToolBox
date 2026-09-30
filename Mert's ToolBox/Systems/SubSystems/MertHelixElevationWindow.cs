using System.Collections.Generic;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using MertsToolBox.Management;
using MertsToolBox.UI;
using MertsToolBox.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using NetElevation = Game.Net.Elevation;

namespace MertsToolBox.Systems
{
    /// <summary>
    /// Pier-like nets (GeometryFlags.RequireElevated) must have one end at least 2 x ElevationLimit high, and the game
    /// checks this per edge (ValidationHelpers.ValidateEdge), so the low entry piece of a helix that climbs high later
    /// fails on its own. For helix edges only, the rule is applied to the whole connected helix instead: when the helix
    /// reaches the required height anywhere, its low edges get the required elevation for the duration of the
    /// validation pass only and are restored right after it. A helix that never leaves the ground still fails.
    /// </summary>
    internal static class MertHelixElevationWindow
    {
        internal struct Pending
        {
            public Entity Edge;
            public bool HadElevation;
            public NetElevation Original;
        }

        internal static readonly List<Pending> PendingRestore = new();
    }

    /// <summary>Runs right before ValidationSystem and lifts the low edges of helixes that reach the required height elsewhere.</summary>
    public partial class MertHelixElevationPrepareSystem : SystemBase
    {
        private const int k_MaxHelixSearch = 1024;
        private const float k_Lift = 0.01f;

        private EntityQuery m_TempEdgeQuery;
        private EntityQuery m_TaggedEdgeQuery;
        private bool m_HelixToolOpen;
        private readonly HashSet<Entity> m_Visited = new();
        private readonly List<Entity> m_Frontier = new();

        /// <summary>Builds the temp edge and helix tag queries.</summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            m_TempEdgeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Edge>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                },
                None = new[] { ComponentType.ReadOnly<Deleted>() },
            });

            m_TaggedEdgeQuery = GetEntityQuery(ComponentType.ReadOnly<MertHelixRoad>(), ComponentType.Exclude<Deleted>());
        }

        /// <summary>
        /// Does nothing unless a preview exists and it can involve a helix: either the helix tool is open, or the city
        /// contains helix roads that a vanilla tool may be editing.
        /// </summary>
        protected override void OnUpdate()
        {
            if (m_TempEdgeQuery.IsEmptyIgnoreFilter)
                return;

            // Helix built with the Anarchy option: Anarchy handles validation, no lift needed.
            if (MertHelixBuildOption.AnarchyBuildActive)
                return;

            m_HelixToolOpen = MertToolState.HelixCleanupRequested;
            if (!m_HelixToolOpen && m_TaggedEdgeQuery.IsEmptyIgnoreFilter)
                return;

            using NativeArray<Entity> edges = m_TempEdgeQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity edge in edges)
            {
                if ((EntityManager.GetComponentData<Temp>(edge).m_Flags & TempFlags.Delete) != 0)
                    continue;

                Entity prefab = EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab;
                if (!EntityManager.HasComponent<NetGeometryData>(prefab))
                    continue;

                NetGeometryData geometry = EntityManager.GetComponentData<NetGeometryData>(prefab);
                if ((geometry.m_Flags & Game.Net.GeometryFlags.RequireElevated) == 0)
                    continue;

                float threshold = geometry.m_ElevationLimit * 2f;
                if (PassesHeightRule(edge, threshold) || !IsHelixEdge(edge) || !HelixReachesHeight(edge, threshold))
                    continue;

                Lift(edge, threshold + k_Lift);
            }
        }

        /// <summary>Sets the edge's elevation to at least the given value for this validation pass and remembers the original.</summary>
        private void Lift(Entity edge, float value)
        {
            var pending = new MertHelixElevationWindow.Pending { Edge = edge };
            float2 lifted = new(value);
            if (EntityManager.HasComponent<NetElevation>(edge))
            {
                pending.HadElevation = true;
                pending.Original = EntityManager.GetComponentData<NetElevation>(edge);
                EntityManager.SetComponentData(edge, new NetElevation { m_Elevation = math.max(pending.Original.m_Elevation, lifted) });
            }
            else
            {
                EntityManager.AddComponentData(edge, new NetElevation { m_Elevation = lifted });
            }
            MertHelixElevationWindow.PendingRestore.Add(pending);
        }

        /// <summary>The game's own rule for one edge (ValidationHelpers.ValidateEdge, RequireElevated branch).</summary>
        private bool PassesHeightRule(Entity edge, float threshold)
        {
            Edge e = EntityManager.GetComponentData<Edge>(edge);
            float2 start = ElevationOf(e.m_Start);
            float2 middle = ElevationOf(edge);
            float2 end = ElevationOf(e.m_End);
            return math.all(math.max(math.max(math.cmin(start), math.cmin(end)), middle) >= threshold);
        }

        /// <summary>Net elevation of a node or edge; zero when the component is missing, as in the game's lookup.</summary>
        private float2 ElevationOf(Entity entity)
        {
            return entity != Entity.Null && EntityManager.HasComponent<NetElevation>(entity)
                ? EntityManager.GetComponentData<NetElevation>(entity).m_Elevation
                : float2.zero;
        }

        /// <summary>
        /// Walks the edges connected to this one through their nodes (and the permanent nodes they stand in for),
        /// staying on helix edges, and returns true as soon as one of them passes the height rule.
        /// </summary>
        private bool HelixReachesHeight(Entity startEdge, float threshold)
        {
            m_Visited.Clear();
            m_Frontier.Clear();
            m_Frontier.Add(startEdge);
            int head = 0;

            while (head < m_Frontier.Count && m_Visited.Count < k_MaxHelixSearch)
            {
                Entity edge = m_Frontier[head++];
                if (!m_Visited.Add(edge) || !IsLiveEdge(edge))
                    continue;
                if (edge != startEdge)
                {
                    if (!IsHelixEdge(edge))
                        continue;
                    if (PassesHeightRule(edge, threshold))
                        return true;
                }

                Edge e = EntityManager.GetComponentData<Edge>(edge);
                AddConnectedEdges(e.m_Start);
                AddConnectedEdges(e.m_End);
                AddConnectedEdges(OriginalOf(e.m_Start));
                AddConnectedEdges(OriginalOf(e.m_End));
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

        /// <summary>An existing edge that is neither deleted nor a temp marked for deletion.</summary>
        private bool IsLiveEdge(Entity edge)
        {
            if (edge == Entity.Null || !EntityManager.Exists(edge) || !EntityManager.HasComponent<Edge>(edge)
                || EntityManager.HasComponent<Deleted>(edge))
                return false;
            return !EntityManager.HasComponent<Temp>(edge)
                || (EntityManager.GetComponentData<Temp>(edge).m_Flags & TempFlags.Delete) == 0;
        }

        /// <summary>
        /// A helix edge: tagged (directly or through the permanent edge a temp copy stands in for), or - while the helix
        /// tool is open - a newly created temp edge of its preview (the tag is only added once the helix is built).
        /// </summary>
        private bool IsHelixEdge(Entity edge)
        {
            if (EntityManager.HasComponent<MertHelixRoad>(edge))
                return true;
            if (!EntityManager.HasComponent<Temp>(edge))
                return false;

            Temp temp = EntityManager.GetComponentData<Temp>(edge);
            if (temp.m_Original != Entity.Null)
                return EntityManager.Exists(temp.m_Original) && EntityManager.HasComponent<MertHelixRoad>(temp.m_Original);
            return m_HelixToolOpen && (temp.m_Flags & TempFlags.Create) != 0;
        }

        /// <summary>Returns the permanent entity a temp entity stands in for, or Entity.Null.</summary>
        private Entity OriginalOf(Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity) || !EntityManager.HasComponent<Temp>(entity))
                return Entity.Null;
            return EntityManager.GetComponentData<Temp>(entity).m_Original;
        }
    }

    /// <summary>Runs right after ValidationSystem: puts back the elevation every lifted edge had before validation.</summary>
    public partial class MertHelixElevationRestoreSystem : SystemBase
    {
        /// <summary>Restores or removes the temporary edge elevations.</summary>
        protected override void OnUpdate()
        {
            List<MertHelixElevationWindow.Pending> pending = MertHelixElevationWindow.PendingRestore;
            if (pending.Count == 0)
                return;

            foreach (MertHelixElevationWindow.Pending p in pending)
            {
                if (!EntityManager.Exists(p.Edge))
                    continue;

                if (p.HadElevation)
                    EntityManager.SetComponentData(p.Edge, p.Original);
                else if (EntityManager.HasComponent<NetElevation>(p.Edge))
                    EntityManager.RemoveComponent<NetElevation>(p.Edge);
            }

            pending.Clear();
        }
    }
}
