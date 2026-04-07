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
        private bool m_ApplyActionSearched = false;
        #endregion

        #region Action & Input Retrieval
        /// <summary>
        /// Resolves and retrieves the appropriate proxy action for applying changes.
        /// </summary>
        protected Game.Input.ProxyAction GetApplyActionLegal()
        {
            if (m_ApplyActionSearched) return m_CachedApplyAction;

            m_ApplyActionSearched = true;
            if (m_ToolSystem == null) return null;

            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Public;
            var fieldInfo = typeof(Game.Tools.ToolSystem).GetField("m_ApplyAction", flags) ??
                            typeof(Game.Tools.ToolSystem).GetField("applyAction", flags);

            if (fieldInfo != null)
            {
                m_CachedApplyAction = fieldInfo.GetValue(m_ToolSystem) as Game.Input.ProxyAction;
                return m_CachedApplyAction;
            }
            var propInfo = typeof(Game.Tools.ToolSystem).GetProperty("applyAction", flags);
            if (propInfo != null)
            {
                m_CachedApplyAction = propInfo.GetValue(m_ToolSystem) as Game.Input.ProxyAction;
            }

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

            if (RealtimeNow < m_SuppressPlacementUntil)
                return;

            bool isMouseOverUI =
                Game.Input.InputManager.instance != null &&
                Game.Input.InputManager.instance.mouseOverUI;

            if (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame &&
                !isMouseOverUI)
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
            return MertToolState.LastResolvedCategory;
        }

        /// <summary>
        /// Captures the contextual data needed to restore the UI state upon tool exit.
        /// </summary>
        protected void CaptureLaunchRestoreContext()
        {
            NetPrefab currentRoad = TryGetCurrentSelectedRoadPrefab();
            Entity currentCategory = GetCurrentlySelectedCategoryEntity();

            if (currentCategory == Entity.Null)
            {
                currentCategory = ResolveCategoryFromRoadPrefab(currentRoad);
            }

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

            if (exitMode == ToolExitMode.SilentTabClose)
                return;

            if (exitMode == ToolExitMode.UserSelectionClose)
                return;


            if (ShouldRestoreLaunchContext(exitMode))
            {
                NetPrefab road = MertToolState.LaunchRoadPrefab;
                Entity category = MertToolState.LaunchCategory;

                MertToolState.QueueRestore(exitMode, road, category);

                var defaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
                if (m_ToolSystem != null && defaultTool != null)
                {
                    m_ToolSystem.selected = Entity.Null;
                    m_ToolSystem.activeTool = defaultTool;
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
        /// Invokes the toolbar UI system to select a specific asset category.
        /// </summary>
        protected void InvokeToolbarSelectAssetCategory(Entity category)
        {
            if (category == Entity.Null)
                return;

            var toolbarUISystem = World.GetExistingSystemManaged<Game.UI.InGame.ToolbarUISystem>();
            if (toolbarUISystem == null)
                return;

            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Public;

            var method = toolbarUISystem.GetType().GetMethod("SelectAssetCategory", flags, null, new Type[] { typeof(Entity) }, null);
            method?.Invoke(toolbarUISystem, new object[] { category });
        }

        /// <summary>
        /// Invokes the toolbar UI system to select a specific asset entity.
        /// </summary>
        protected void InvokeToolbarSelectAsset(Entity assetEntity, bool updateTool)
        {
            if (assetEntity == Entity.Null)
                return;

            var toolbarUISystem = World.GetExistingSystemManaged<Game.UI.InGame.ToolbarUISystem>();
            if (toolbarUISystem == null)
                return;

            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Public;

            var method = toolbarUISystem.GetType().GetMethod("SelectAsset", flags, null, new Type[] { typeof(Entity), typeof(bool) }, null);
            method?.Invoke(toolbarUISystem, new object[] { assetEntity, updateTool });
        }
        #endregion
    }
}