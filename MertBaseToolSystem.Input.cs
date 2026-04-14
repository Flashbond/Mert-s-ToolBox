using Colossal.Entities;
using Game.Prefabs;
using Game.Tools;
using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MertsToolBox
{
    public abstract partial class MertBaseToolSystem
    {
        #region Fields & State
        protected double m_InputCooldown = 0f;

        private Game.Input.ProxyAction m_CachedApplyAction;
        #endregion

        #region Action & Input Retrieval
        /// <summary>
        /// Resolves and retrieves the appropriate proxy action for applying changes directly from InputManager.
        /// </summary>
        protected Game.Input.ProxyAction GetApplyActionLegal()
        {
            if (m_CachedApplyAction != null)
                return m_CachedApplyAction;

            if (Game.Input.InputManager.instance == null)
                return null;

            m_CachedApplyAction = Game.Input.InputManager.instance.FindAction(
                Game.Input.InputManager.kToolMap,
                "Apply"
            );

            return m_CachedApplyAction;
        }

        /// <summary>
        /// Gets the current mouse scroll direction while applying a cooldown mechanism.
        /// </summary>
        protected int GetScrollDirection()
        {
            if (Mouse.current == null) return 0;
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f)
                return 0;

            return wheel > 0 ? 1 : -1;
        }

        /// <summary>
        /// Monitors inputs to gracefully exit the tool or confirm placement when appropriate.
        /// </summary>
        protected void CheckExitAndPlacementInputs()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ExecuteGracefulExit(ToolExitMode.RestoreFromEscape);
                return;
            }
            
            Game.Input.ProxyAction applyAction = GetApplyActionLegal();
        
            if (applyAction != null && applyAction.WasPerformedThisFrame())
            {
                if (m_ToolRaycastSystem != null &&
                    m_ToolRaycastSystem.GetRaycastResult(out var result))
                {
                    OnShapePlaced();
                    ExecuteGracefulExit(ToolExitMode.RestoreFromPlacement);
                }
            }
        }
        #endregion

        #region Prefab Validation
        /// <summary>
        /// Validates if the currently selected prefab is a valid network tool prefab.
        /// </summary>
        public bool IsCurrentPrefabValid()
        {
            if (m_NetToolSystem == null) return false;
            return IsStandardRoadPrefab(m_NetToolSystem.GetPrefab());
        }

        /// <summary>
        /// Checks whether a given prefab represents a standard road, excluding unsupported types.
        /// </summary>
        protected bool IsStandardRoadPrefab(PrefabBase prefabToTest)
        {
            if (prefabToTest is not RoadPrefab) return false;

            string name = prefabToTest.name.ToLowerInvariant();

            if (name.Contains("bridge") || name.Contains("quay") || name.Contains("pedestrian") ||
                name.Contains("public transport") || name.Contains("alley") || name.Contains("gravel") ||
                name.Contains("dirt") || name.Contains("roundabout")) return false;

            if (name.Contains("parking"))
            {
                Entity prefabEntity = m_PrefabSystem.GetEntity(prefabToTest);
                if (prefabEntity != Entity.Null && !EntityManager.HasComponent<RoadData>(prefabEntity)) return false;
            }

            return name.Contains("road") || name.Contains("highway");
        }
        #endregion

        #region Tool State & Lifecycle
        /// <summary>
        /// Retrieves the entity corresponding to the currently selected asset category.
        /// </summary>
        protected Entity GetCurrentlySelectedCategoryEntity()
        {
            if (MertToolState.LastResolvedCategory != Entity.Null)
                return MertToolState.LastResolvedCategory;

            NetPrefab currentRoad = TryGetCurrentSelectedRoadPrefab();
            Entity resolved = ResolveCategoryFromRoadPrefab(currentRoad);
            if (resolved != Entity.Null)
                return resolved;

            return Entity.Null;
        }
        /// <summary>
        /// Safely retrieves the currently selected road prefab from the toolbar UI.
        /// </summary>
        protected NetPrefab TryGetCurrentSelectedRoadPrefab()
        {
            try
            {
                if (m_NetToolSystem == null)
                    return MertToolState.LastResolvedRoadPrefab;

                if (m_SelectedPrefabField != null)
                {
                    if (m_SelectedPrefabField.GetValue(m_NetToolSystem) is NetPrefab selectedPrefab && selectedPrefab != null)
                        return selectedPrefab;
                }

                if (m_PrefabField != null)
                {
                    if (m_PrefabField.GetValue(m_NetToolSystem) is NetPrefab prefab && prefab != null)
                        return prefab;
                }

                if (m_NetToolSystem.GetPrefab() is NetPrefab publicPrefab)
                    return publicPrefab;
            }
            catch
            {
            }

            return MertToolState.LastResolvedRoadPrefab;
        }

        /// <summary>
        /// Captures the contextual data needed to restore the UI state upon tool exit.
        /// </summary>
        protected void CaptureLaunchRestoreContext()
        {
            NetPrefab currentRoad = TryGetCurrentSelectedRoadPrefab();
            Entity currentCategory = GetCurrentlySelectedCategoryEntity();

            NetPrefab launchRoad = currentRoad ?? MertToolState.LastResolvedRoadPrefab;
            Entity launchCategory = currentCategory != Entity.Null
                ? currentCategory
                : MertToolState.LastResolvedCategory;

            MertToolState.CaptureLaunchContext(launchRoad, launchCategory);
            MertToolState.RememberRoadForCategory(launchCategory, launchRoad);
        }

        /// <summary>
        /// Determines whether the given exit mode warrants restoring the launch context.
        /// </summary>
        protected bool ShouldRestoreLaunchContext(ToolExitMode exitMode)
        {
            return exitMode == ToolExitMode.RestoreFromEscape
                || exitMode == ToolExitMode.RestoreFromPlacement;
        }

        /// <summary>
        /// Manages the graceful shutdown process of the tool and orchestrates state restoration.
        /// </summary>
        protected virtual void ExecuteGracefulExit(ToolExitMode exitMode)
        {
            ToolEnabled = false;
            ResetRuntimeStamp();

            try
            {
                if (m_ObjectToolSystem != null)
                {
                    ModRuntime.TrySetField(m_ObjectToolSystem, "m_SelectedPrefab", null);
                    ModRuntime.TrySetField(m_ObjectToolSystem, "m_Prefab", null);
                }
            }
            catch
            {
            }

            if (exitMode == ToolExitMode.SilentTabClose)
            {
                RefreshRoadToolbarSelectionWithoutToolSwitch();
                return;
            }

            if (exitMode == ToolExitMode.UserSelectionClose)
                return;

            if (ShouldRestoreLaunchContext(exitMode))
            {
                NetPrefab road = MertToolState.LaunchRoadPrefab;
                Entity category = MertToolState.LaunchCategory;

                MertToolState.QueueRestore(exitMode, road, category);

                var netTool = World.GetOrCreateSystemManaged<NetToolSystem>();
                if (m_ToolSystem != null && netTool != null)
                {
                    m_ToolSystem.selected = Entity.Null;
                    m_ToolSystem.activeTool = netTool;
                }

                return;
            }
        }

        /// <summary>
        /// Resets the geometrical data associated with the runtime stamp entity.
        /// </summary>
        protected void ResetRuntimeStamp()
        {
            if (m_RuntimeStamp == null) return;

            if (m_RuntimeStamp.TryGet<ObjectSubNets>(out ObjectSubNets subNets))
            {
                subNets.m_SubNets = Array.Empty<ObjectSubNetInfo>();
            }

            if (RuntimeStampEntity != Entity.Null && EntityManager.Exists(RuntimeStampEntity))
            {
                if (EntityManager.TryGetComponent(RuntimeStampEntity, out ObjectGeometryData geom))
                {
                    geom.m_Size = float3.zero;
                    EntityManager.SetComponentData(RuntimeStampEntity, geom);
                }
            }
            m_RuntimeStamp.asset?.MarkDirty();
        }
        #endregion

        #region Reflection & Utilities
        /// <summary>
        /// Resolves the corresponding category entity linked to the provided road prefab.
        /// </summary>
        protected Entity ResolveCategoryFromRoadPrefab(NetPrefab roadPrefab)
        {
            if (roadPrefab == null || m_PrefabSystem == null)
                return Entity.Null;

            Entity roadEntity = m_PrefabSystem.GetEntity(roadPrefab);
            if (roadEntity == Entity.Null || !EntityManager.Exists(roadEntity))
                return Entity.Null;

            if (EntityManager.TryGetComponent(roadEntity, out UIObjectData uiObject) &&
                uiObject.m_Group != Entity.Null)
            {
                return uiObject.m_Group;
            }

            return Entity.Null;
        }

        /// <summary>
        /// Silently restores the vanilla toolbar's asset selection memory and releases object tool locks to prevent UI desyncs during category transitions.
        /// </summary>
        protected void RefreshRoadToolbarSelectionWithoutToolSwitch()
        {
            try
            {
                if (m_PrefabSystem == null)
                    return;

                NetPrefab road = MertToolState.LaunchRoadPrefab ?? MertToolState.LastResolvedRoadPrefab;
                if (road == null)
                    return;

                Entity roadEntity = m_PrefabSystem.GetEntity(road);
                if (roadEntity == Entity.Null)
                    return;

                var toolbarUISystem = World.GetExistingSystemManaged<Game.UI.InGame.ToolbarUISystem>();
                if (toolbarUISystem == null)
                    return;

                var flags = System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic;

                var selectAsset = toolbarUISystem.GetType()
                    .GetMethod("SelectAsset", flags, null, new Type[] { typeof(Entity), typeof(bool) }, null);

                if (selectAsset == null)
                    return;

                MertToolState.SuppressUiMemoryCapture = true;
                MertToolState.SuppressCategoryCapture = true;

                try
                {
                    // updateTool=false: toolbar highlight/hafıza düzelsin, tool zorlanmasın
                    selectAsset.Invoke(toolbarUISystem, new object[] { roadEntity, false });
                }
                finally
                {
                    MertToolState.SuppressUiMemoryCapture = false;
                    MertToolState.SuppressCategoryCapture = false;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[MertsToolBox] RefreshRoadToolbarSelectionWithoutToolSwitch failed: " + e.Message);
            }
        }
        #endregion
    }
}