using Colossal.Entities;
using Game.Prefabs;
using Game.UI.InGame;
using MertsToolBox.Core;
using MertsToolBox.Management;
using MertsToolBox.Systems.SubSystems;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MertsToolBox
{
    public struct MertRoadProfile
    {
        public float Width;
        public bool IsReliable;
    }
    public abstract partial class MertBaseToolSystem
    {
        #region Fields & State

        private Game.Input.ProxyAction m_CachedApplyAction;
        private InputAction m_SourceElevationAction;
        private InputAction m_ShadowElevationAction;
        private bool m_SourceElevationWasEnabled;
        private bool m_VanillaElevationSuppressed;
        private static readonly Dictionary<Entity, MertRoadProfile> s_RoadProfileCache = new();

        private static FieldInfo s_BindingField;
        private static PropertyInfo s_ValueProperty;

        private static readonly float[] s_ElevationStepValues = new float[]
        {
            10f, 5f, 2.5f, 1.25f
        };
        private enum MertNetKind
        {
            Unknown,
            Road,
            Track,
            Pier
        }

        private struct MertRoadProfile
        {
            public bool IsReliable;
            public float Width;
            public MertNetKind Kind;
        }
        public float[] GetElevationStepArray()
        {
            return s_ElevationStepValues;
        }

        public float GetElevationStepValue()
        {
            return m_NetToolSystem != null ? m_NetToolSystem.elevationStep : 10f;
        }

        public float GetCurrentElevationValue()
        {
            return m_NetToolSystem != null ? m_NetToolSystem.elevation : 0f;
        }

        public void SetElevationStepFromUi(float value)
        {
            if (m_NetToolSystem == null)
                return;

            if (value > 7.5f)
                m_NetToolSystem.elevationStep = 10f;
            else if (value > 3.75f)
                m_NetToolSystem.elevationStep = 5f;
            else if (value > 1.875f)
                m_NetToolSystem.elevationStep = 2.5f;
            else
                m_NetToolSystem.elevationStep = 1.25f;
        }
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
        protected virtual void ProcessElevationInput()
        {
            if (!ToolEnabled)
                return;

            if (m_ShadowElevationAction == null)
                return;

            if (!m_ShadowElevationAction.WasPerformedThisFrame())
                return;

            float value = m_ShadowElevationAction.ReadValue<float>();

            if (value > 0.1f)
                RouteElevationToNetTool(+1);
            else if (value < -0.1f)
                RouteElevationToNetTool(-1);

        }

        protected void DisableVanillaElevation()
        {
            if (m_VanillaElevationSuppressed)
                return;

            try
            {
                var im = Game.Input.InputManager.instance;
                if (im == null)
                    return;

                Game.Input.ProxyAction proxyAction = null;

                var prop = im.GetType().GetProperty(
                    "keyActionMap",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );


                if (prop?.GetValue(im) is Dictionary<string, HashSet<Game.Input.ProxyAction>> keyMap)
                {
                    foreach (var kv in keyMap)
                    {
                        foreach (var action in kv.Value)
                        {
                            if (action != null && action.name == "Change Elevation")
                            {
                                proxyAction = action;
                                break;
                            }
                        }

                        if (proxyAction != null)
                            break;
                    }
                }

                if (proxyAction == null)
                {
                    ModRuntime.Warn("[INPUT] Change Elevation ProxyAction not found");
                    return;
                }

                var sourceField = proxyAction.GetType().GetField(
                    "m_SourceAction",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );

                m_SourceElevationAction = sourceField?.GetValue(proxyAction) as InputAction;

                if (m_SourceElevationAction == null)
                {
                    ModRuntime.Warn("[INPUT] Source elevation action not found");
                    return;
                }

                m_ShadowElevationAction = m_SourceElevationAction.Clone();
                m_ShadowElevationAction.Enable();

                m_SourceElevationWasEnabled = m_SourceElevationAction.enabled;

                if (m_SourceElevationAction.enabled)
                    m_SourceElevationAction.Disable();

                m_VanillaElevationSuppressed = true;
            }
            catch (Exception e)
            {
                ModRuntime.Warn("[INPUT] DisableVanillaElevation failed: " + e.Message);
            }
        }

        protected void RestoreVanillaElevation()
        {
            if (!m_VanillaElevationSuppressed)
                return;

            try
            {
                if (m_ShadowElevationAction != null)
                {
                    m_ShadowElevationAction.Disable();
                    m_ShadowElevationAction.Dispose();
                }

                if (m_SourceElevationAction != null && m_SourceElevationWasEnabled)
                {
                    if (!m_SourceElevationAction.enabled)
                        m_SourceElevationAction.Enable();
                }
            }
            catch (Exception e)
            {
                ModRuntime.Warn("[INPUT] RestoreVanillaElevation failed: " + e.Message);
            }
            finally
            {
                m_ShadowElevationAction = null;
                m_SourceElevationAction = null;
                m_SourceElevationWasEnabled = false;
                m_VanillaElevationSuppressed = false;
            }
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
        protected void CheckPlacementInputs()
        {
            Game.Input.ProxyAction applyAction = GetApplyActionLegal();

            if (applyAction != null && applyAction.WasPerformedThisFrame())
            {
                if (m_ToolRaycastSystem != null &&  m_ToolRaycastSystem.GetRaycastResult(out _))
                {
                    MertPlacementGhostSuppressSystem.RequestOnce();
                    Systems.MertTrafficLightSuppressSystem.MarkNodesForPlacement();
                    RequestDisable(ToolExitMode.RestoreFromPlacement);
                }
            }
        }
        #endregion

        #region Prefab Validation
        /// <summary>
        /// Validates that the currently active NetTool prefab is a supported road AND that the
        /// vanilla toolbar selection points to the exact same asset entity. This prevents stale
        /// toolbar highlights from falsely enabling the toolbox after external selections
        /// (e.g. Road Builder browser selections in the same category).
        /// </summary>
        public bool IsCurrentPrefabValid()
        {
            if (m_NetToolSystem == null)
                return false;

            if (m_NetToolSystem.GetPrefab() is not NetPrefab currentNetPrefab || currentNetPrefab == null)
                return false;

            Entity currentPrefabEntity = m_PrefabSystem.GetEntity(currentNetPrefab);

            if (currentPrefabEntity == Entity.Null || !EntityManager.Exists(currentPrefabEntity))
                return false;

            if (!TryGetVanillaSelectedAssetEntity(out Entity selectedAssetEntity))
                return false;

            if (selectedAssetEntity != currentPrefabEntity)
                return false;

            return IsStandardRoadPrefab(currentNetPrefab);
        }

        /// <summary>
        /// Checks whether a given prefab represents a standard road using ECS attributes, dynamic width, and hybrid caching.
        /// </summary>
        protected bool IsStandardRoadPrefab(PrefabBase prefabToTest)
        {
            if (prefabToTest is not NetPrefab netPrefab)
                return false;

            Entity prefabEntity = m_PrefabSystem.GetEntity(prefabToTest);
            if (prefabEntity == Entity.Null || !EntityManager.Exists(prefabEntity))
                return false;

            if (!s_RoadProfileCache.TryGetValue(prefabEntity, out MertRoadProfile profile))
            {
                profile = new MertRoadProfile
                {
                    IsReliable = false,
                    Width = 0f,
                    Kind = MertNetKind.Unknown
                };

                bool hasInvalidDNA = false;
                string matchedDNAInfo = "";

                string internalName = netPrefab.name?.ToLowerInvariant() ?? "";

                if (internalName.Contains("quay") ||
                    internalName.Contains("retaining") ||
                    internalName.Contains("dam") ||
                    internalName.Contains("bridge"))
                {
                    hasInvalidDNA = true;
                    matchedDNAInfo += $"[Name={internalName}] ";
                }

                if (netPrefab is NetGeometryPrefab geometryPrefab &&
                    geometryPrefab.m_Sections != null)
                {
                    foreach (var sectionInfo in geometryPrefab.m_Sections)
                    {
                        if (sectionInfo.m_Section?.m_Pieces == null)
                            continue;

                        foreach (var pieceInfo in sectionInfo.m_Section.m_Pieces)
                        {
                            if (pieceInfo.m_Piece == null)
                                continue;

                            string pieceName = pieceInfo.m_Piece.name?.ToLowerInvariant() ?? "";

                            if (pieceName.Contains("quay") ||
                                pieceName.Contains("retaining") ||
                                pieceName.Contains("dam") ||
                                pieceName.Contains("bridge") ||
                                pieceName.Contains("suspension") ||
                                pieceName.Contains("extradosed") ||
                                pieceName.Contains("truss"))
                            {
                                hasInvalidDNA = true;
                            }
                        }
                    }
                }

                bool isRoad = netPrefab is RoadPrefab;
                bool isTrack = netPrefab is TrackPrefab;

                bool isPier = internalName.Contains("pier");

                if (isPier)
                {
                    profile.Kind = MertNetKind.Pier;
                }
                else if (isTrack)
                {
                    profile.Kind = MertNetKind.Track;
                }
                else if (isRoad)
                {
                    profile.Kind = MertNetKind.Road;
                }

                bool isPlaceableByUser = netPrefab.Has<PlaceableNet>();
                bool hasRoadData = isRoad && EntityManager.HasComponent<RoadData>(prefabEntity);

                string rejectionReason = "";

                if (!isPlaceableByUser)
                {
                    rejectionReason = "Not Placeable";
                }
                else if (!isRoad && !isTrack && !isPier)
                {
                    rejectionReason = "Invalid Net Type";
                }
                else if (hasInvalidDNA && !isPier)
                {
                    rejectionReason = $"Invalid DNA Found: {matchedDNAInfo}";
                }
                else if (isRoad && !hasRoadData && !isPier)
                {
                    rejectionReason = "Missing RoadData Component";
                }

                profile.IsReliable = rejectionReason == "";

                s_RoadProfileCache[prefabEntity] = profile;
            }

            return profile.IsReliable;
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
            catch { }

            return MertToolState.LastResolvedRoadPrefab;
        }

        /// <summary>
        /// Captures the contextual data needed to restore the UI state upon tool exit.
        /// </summary>
        protected void CaptureLaunchRestoreContext()
        {
            NetPrefab road = TryGetCurrentSelectedRoadPrefab();

            Entity category = Entity.Null;

            MertToolState.CaptureLaunchContext(road,category);
            MertToolState.CaptureResolvedRoadContext(road,category);
        }

        /// <summary>
        /// Manages the graceful shutdown process of the tool and orchestrates state restoration.
        /// </summary>
        protected virtual void ExecuteGracefulExit(ToolExitMode exitMode)
        {
            RestoreVanillaElevation();
            ClearUndoHistory();

            if (MertToolState.ActiveTool == this)
                MertToolState.ActiveTool = null;

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
            catch { }

            if (exitMode == ToolExitMode.UserSelectionClose) return;

            if (exitMode == ToolExitMode.VanillaToolbarClear)  return;

            if (exitMode == ToolExitMode.RestoreFromPlacement)
            {
                RestoreLaunchRoadViaSelectAsset();
                return;
            }
        }
        private void RestoreLaunchRoadViaSelectAsset()
        {
            NetPrefab road = MertToolState.LaunchRoadPrefab;
            if (road == null)
                return;

            if (!MertToolbarHandoffMemory.TryResolveEntity(road, out Entity roadEntity))
                return;

            var toolbar = World.GetExistingSystemManaged<ToolbarUISystem>();
            if (toolbar == null)
                return;

            MertToolbarReflection.ReplaySelectAsset(
                toolbar,
                roadEntity,
                true);
        }

        /// <summary>
        /// Resets the geometrical data associated with the runtime stamp entity.
        /// </summary>
        protected void ResetRuntimeStamp()
        {
            if (m_RuntimeStamp == null)
                return;

            if (m_RuntimeStamp.TryGet<ObjectSubNets>(out ObjectSubNets subNets))
            {
                subNets.m_SubNets = Array.Empty<ObjectSubNetInfo>();
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
        /// Safely extracts the currently selected asset entity directly from the vanilla toolbar UI using reflection.
        /// </summary>
        protected bool TryGetVanillaSelectedAssetEntity(out Entity selectedAssetEntity)
        {
            selectedAssetEntity = Entity.Null;

            var toolbarSystem = World.DefaultGameObjectInjectionWorld?
                .GetExistingSystemManaged<ToolbarUISystem>();

            if (toolbarSystem == null) return false;

            if (s_BindingField == null)
                s_BindingField = typeof(ToolbarUISystem)
                    .GetField("m_SelectedAssetBinding", BindingFlags.Instance | BindingFlags.NonPublic);

            if (s_BindingField?.GetValue(toolbarSystem) is not object bindingObj)
                return false;

            if (s_ValueProperty == null)
                s_ValueProperty = bindingObj.GetType().GetProperty("value");

            if (s_ValueProperty == null) return false;
            try
            {
                var rawValue = s_ValueProperty.GetValue(bindingObj);
                if (rawValue is Entity entity)
                {
                    selectedAssetEntity = entity;
                }
            }
            catch
            {
                return false;
            }
            return selectedAssetEntity != Entity.Null;
        }
        protected bool IsTrackPrefab(PrefabBase prefab)
        {
            if (prefab is not NetPrefab netPrefab)
                return false;

            return netPrefab is TrackPrefab;
        }

        protected bool IsPierPrefab(PrefabBase prefab)
        {
            if (prefab is not NetPrefab netPrefab)
                return false;

            string name = netPrefab.name?.ToLowerInvariant() ?? "";

            return name.Contains("pier");
        }
        #endregion

        #region Cache & Profile Retrieval
        /// <summary>
        /// Retrieves the exact physical width of the road from the smart cache.
        /// Falls back to estimation if the road is somehow not cached.
        /// </summary>
        protected float GetOrCreateRoadWidth(NetPrefab roadPrefab)
        {
            if (roadPrefab == null)
                return 8f;

            Entity prefabEntity = m_PrefabSystem.GetEntity(roadPrefab);

            if (prefabEntity != Entity.Null &&
                s_RoadProfileCache.TryGetValue(prefabEntity, out MertRoadProfile profile) &&
                profile.Width > 0.1f)
            {
                return profile.Width;
            }

            float width = EstimateRoadWidth(roadPrefab);

            if (prefabEntity != Entity.Null)
            {
                if (!s_RoadProfileCache.TryGetValue(prefabEntity, out profile))
                {
                    profile = new MertRoadProfile();
                }

                profile.Width = width;
                s_RoadProfileCache[prefabEntity] = profile;
            }

            return width;
        }
        #endregion
    }
}