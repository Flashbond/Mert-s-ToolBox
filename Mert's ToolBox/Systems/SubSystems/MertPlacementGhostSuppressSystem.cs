using Game.Common;
using Game.Tools;
using Unity.Entities;

namespace MertsToolBox.Systems.SubSystems
{
    /// <summary>
    /// Removes the "second stamp" ghost after a placement.
    /// When the object tool applies a placement it immediately writes the definitions of the NEXT placement in the same
    /// frame (ObjectToolSystem.Apply: applyMode = Apply, then GetRaycastResult + UpdateDefinitions), so that the player
    /// can keep placing. Our tool exits right after the placement, but the generate systems would still turn those
    /// definitions into a one-frame preview (marking them Deleted is not enough, they are still read). So this frame's
    /// fresh definition entities are removed before GenerateObjectsSystem runs.
    /// Definition entities are short-lived tool output (not prefabs, not city objects); the tool recreates them whenever
    /// it needs a preview. The placement itself is applied from the existing temp entities and is not affected.
    ///
    /// The system is disabled by default, so the game never calls its OnUpdate. RequestOnce enables it in the frame of a
    /// placement-exit; it does its work once in that same frame and disables itself again.
    /// </summary>
    public partial class MertPlacementGhostSuppressSystem : SystemBase
    {
        private static MertPlacementGhostSuppressSystem s_Instance;

        private EntityQuery m_DefinitionQuery;

        /// <summary>Call in the frame a placement is applied and our tool is about to exit.</summary>
        public static void RequestOnce()
        {
            if (s_Instance != null)
                s_Instance.Enabled = true;
        }

        /// <summary>Builds the query of fresh creation definitions and starts disabled.</summary>
        protected override void OnCreate()
        {
            base.OnCreate();
            s_Instance = this;
            // Only this frame's fresh definitions; older ones already marked Deleted are left to the game.
            m_DefinitionQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<CreationDefinition>() },
                None = new[] { ComponentType.ReadOnly<Deleted>() },
            });
            Enabled = false;
        }

        /// <summary>Runs only in the placement-exit frame: removes the "next placement" definitions, then switches off.</summary>
        protected override void OnUpdate()
        {
            Enabled = false;
            if (!m_DefinitionQuery.IsEmptyIgnoreFilter)
                EntityManager.DestroyEntity(m_DefinitionQuery);
        }

        /// <summary>Drops the static reference.</summary>
        protected override void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
            base.OnDestroy();
        }
    }
}
