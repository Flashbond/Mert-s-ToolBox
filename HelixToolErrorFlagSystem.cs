using Game;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace MertsToolBox
{
    public partial class HelixToolErrorFlagSystem : GameSystemBase
    {
        #region 1. QUERIES & STATE

        private EntityQuery m_ToolErrorPrefabQuery;

        #endregion

        #region 2. ECS LIFECYCLE

        /// <summary>
        /// Initializes the entity query required to find tool error prefabs and sets update requirements.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolErrorPrefabQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<ToolErrorData>(),
                    ComponentType.ReadOnly<NotificationIconData>()
                }
            });

            RequireForUpdate(m_ToolErrorPrefabQuery);
        }

        /// <summary>
        /// Evaluates the Helix tool's active state and dynamically suppresses the 'OverlapExisting' error flag to allow overlapping road networks.
        /// </summary>
        protected override void OnUpdate()
        {
            bool helixActive = MertToolState.HelixCleanupRequested;

            using var prefabs = m_ToolErrorPrefabQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in prefabs)
            {
                var data = EntityManager.GetComponentData<ToolErrorData>(entity);

                if (data.m_Error != ErrorType.OverlapExisting)
                    continue;

                var flags = data.m_Flags;

                if (helixActive)
                {
                    flags |= ToolErrorFlags.DisableInGame;
                    flags |= ToolErrorFlags.DisableInEditor;
                }
                else
                {
                    flags &= ~ToolErrorFlags.DisableInGame;
                    flags &= ~ToolErrorFlags.DisableInEditor;
                }

                if (flags != data.m_Flags)
                {
                    data.m_Flags = flags;
                    EntityManager.SetComponentData(entity, data);
                }
            }
        }

        #endregion
    }
}