using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using MertsToolBox.Management;
using Unity.Collections;
using Unity.Entities;

namespace MertsToolBox.Systems
{
    /// <summary>
    /// Suppresses traffic lights on the intersections of our stamps by applying the same node upgrade the game's own
    /// "remove traffic lights" intersection upgrade applies: Game.Net.Upgraded on the node with
    /// CompositionFlags.General.RemoveTrafficLights. The game then drops the TrafficLights component and the light
    /// poles itself; nothing is deleted by us.
    ///
    /// The flag is written ONLY in the frame a placement is applied, not while previewing. Writing it on the preview
    /// forced the game to re-update the preview nodes outside its normal generation pass every time the stamp moved,
    /// which dropped the white "will be removed" outlines of trees/buildings under the stamp (flicker, then gone).
    /// In the placement frame the object tool has already decided to apply (it runs before our tool in ToolUpdate) and
    /// the game converts these temp nodes to permanent ones later in the same frame (ApplyTool phase), keeping the
    /// upgrade - the same path that was tested to survive save/load and mod removal.
    /// Trade-off: the preview still shows the lights; the built intersections have none.
    /// </summary>
    public partial class MertTrafficLightSuppressSystem : SystemBase
    {
        private static MertTrafficLightSuppressSystem s_Instance;

        private EntityQuery m_TempNodeQuery;

        /// <summary>
        /// Call from the placement check, in the frame the object tool applies the stamp, before the tool exits.
        /// Does nothing unless a tool that supports it (Grid) is active and its option is on.
        /// </summary>
        public static void MarkNodesForPlacement()
        {
            if (s_Instance == null || !MertToolState.TrafficLightToolActive || !MertToolState.SuppressTrafficLights)
                return;

            s_Instance.MarkNewTempNodes();
        }

        /// <summary>Builds the temp node query. The system never updates on its own.</summary>
        protected override void OnCreate()
        {
            base.OnCreate();
            s_Instance = this;
            m_TempNodeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Node>(), ComponentType.ReadOnly<Temp>() },
                None = new[] { ComponentType.ReadOnly<Deleted>() },
            });
            Enabled = false;
        }

        /// <summary>Never called (the system is disabled); the work is done on demand by MarkNodesForPlacement.</summary>
        protected override void OnUpdate()
        {
        }

        /// <summary>Drops the static reference.</summary>
        protected override void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
            base.OnDestroy();
        }

        /// <summary>Marks the new nodes of the preview being applied "remove traffic lights".</summary>
        private void MarkNewTempNodes()
        {
            if (m_TempNodeQuery.IsEmptyIgnoreFilter)
                return;

            using NativeArray<Entity> nodes = m_TempNodeQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity node in nodes)
            {
                // Only nodes our stamp creates. Existing nodes it connects to (copies with an original) keep whatever
                // the player chose for them.
                Temp temp = EntityManager.GetComponentData<Temp>(node);
                if ((temp.m_Flags & TempFlags.Create) == 0 || (temp.m_Flags & TempFlags.Delete) != 0 || temp.m_Original != Entity.Null)
                    continue;

                if (EntityManager.HasComponent<Upgraded>(node))
                {
                    Upgraded upgraded = EntityManager.GetComponentData<Upgraded>(node);
                    if ((upgraded.m_Flags.m_General & CompositionFlags.General.RemoveTrafficLights) != 0)
                        continue;
                    upgraded.m_Flags.m_General |= CompositionFlags.General.RemoveTrafficLights;
                    EntityManager.SetComponentData(node, upgraded);
                }
                else
                {
                    var upgraded = new Upgraded();
                    upgraded.m_Flags.m_General = CompositionFlags.General.RemoveTrafficLights;
                    EntityManager.AddComponentData(node, upgraded);
                }
            }
        }
    }
}
