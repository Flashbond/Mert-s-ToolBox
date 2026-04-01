using Game.Prefabs;
using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MertsToolBox
{
    public abstract partial class MertBaseToolSystem
    {
        #region INPUT & PREFAB FILTERING

        /// <summary>
        /// Detects the mouse wheel scroll direction with a small cooldown to prevent input jitter.
        /// </summary>
        protected int GetScrollDirection()
        {
            if (Keyboard.current == null || Mouse.current == null)
                return 0;

            float wheel = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f)
                return 0;

            if (UnityEngine.Time.realtimeSinceStartup < m_InputCooldown)
                return 0;

            m_InputCooldown = UnityEngine.Time.realtimeSinceStartup + 0.04f;
            return wheel > 0 ? 1 : -1;
        }

        /// <summary>
        /// Checks if the currently selected prefab in the Net Tool is a valid road for this tool.
        /// </summary>
        public bool IsCurrentPrefabValid()
        {
            if (m_NetToolSystem == null)
                return false;

            PrefabBase currentPrefab = m_NetToolSystem.GetPrefab();
            return IsStandardRoadPrefab(currentPrefab);
        }

        /// <summary>
        /// Validates the road prefab, excluding specialized structures but allowing roads with parking components.
        /// </summary>
        protected bool IsStandardRoadPrefab(PrefabBase prefabToTest)
        {
            if (prefabToTest == null)
                return false;

            if (prefabToTest is not RoadPrefab)
                return false;

            string name = prefabToTest.name.ToLowerInvariant();

            // Blacklist specialized net types
            if (name.Contains("bridge") ||
                name.Contains("quay") ||
                name.Contains("pedestrian") ||
                name.Contains("public transport") ||
                name.Contains("alley") ||
                name.Contains("gravel") ||
                name.Contains("dirt") ||
                name.Contains("roundabout"))
            {
                return false;
            }

            // Parking roads: allow only if they are real road prefabs with RoadData
            if (name.Contains("parking"))
            {
                Entity prefabEntity = m_PrefabSystem.GetEntity(prefabToTest);
                if (prefabEntity != Entity.Null && !EntityManager.HasComponent<RoadData>(prefabEntity))
                    return false;
            }

            return name.Contains("road") || name.Contains("highway");
        }

        /// <summary>
        /// Handles the Escape key logic to trigger tool deactivation.
        /// </summary>
        private bool HandleEscapeExit()
        {
            if (m_CancelAction != null && m_CancelAction.WasPressedThisFrame())
            {
                DisableToolMode(ToolExitMode.RestoreFromEscape);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles the placement click and delayed disable after placement.
        /// Uses realtime instead of simulation time so the visual feel stays responsive under load.
        /// </summary>
        private bool HandleDeferredDisable()
        {
            // Protect against UI clicks leaking into world placement
            bool isMouseOverUI =
                (DateTime.UtcNow - MertToolBoxUISystem.m_LastUiInteractionTime).TotalMilliseconds < 250;

            if (!m_PendingDisableAfterPlacement &&
                RealtimeNow >= m_SuppressAutoDisableUntil &&
                m_ToolSystem.activeTool == m_ObjectToolSystem &&
                Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (isMouseOverUI)
                    return false;

                m_PendingDisableAfterPlacement = true;
                m_PostPlaceDisableAt = RealtimeNow + 0.01;
            }

            if (m_PendingDisableAfterPlacement && RealtimeNow >= m_PostPlaceDisableAt)
            {
                m_PendingDisableAfterPlacement = false;

                if (!ToolEnabled || m_ToolSystem.activeTool != m_ObjectToolSystem)
                    return false;

                DisableToolMode(ToolExitMode.RestoreFromPlacement);
                return true;
            }

            return false;
        }
        protected void ArmDisableAfterSuccessfulPlacement(double delay = 0.01)
        {
            m_PendingDisableAfterPlacement = true;
            m_PostPlaceDisableAt = RealtimeNow + delay;
        }
        #endregion
    }
}