using Colossal.Entities;
using Game;
using Game.Prefabs;
using Game.Tools;
using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace MertsToolBox
{
    public abstract partial class MertBaseToolSystem : GameSystemBase
    {
        #region 1. SYSTEM REFERENCES

        protected ToolSystem m_ToolSystem;
        protected NetToolSystem m_NetToolSystem;
        protected ObjectToolSystem m_ObjectToolSystem;
        protected PrefabSystem m_PrefabSystem;
        protected ToolRaycastSystem m_ToolRaycastSystem;

        #endregion

        #region 2. TOOL STATE

        public bool ToolEnabled { get; private set; }

        protected virtual bool RequiresSnapEnforcement => false;
        protected virtual bool AllowOverlapPlacement => false;

        protected double RealtimeNow => UnityEngine.Time.realtimeSinceStartupAsDouble;

        #endregion

        #region 3. CREATE / REBUILD PIPELINE

        protected bool m_PendingCreateShape;
        protected bool m_PreviewRebuildQueued;
        protected double m_PreviewRebuildAt;
        protected const double PreviewRebuildDelay = 0.04;

        private bool m_ExecuteCreateShape;
        private bool m_IsCreatingShape;
        private double m_LastCreateTime;

        #endregion

        #region 4. HANDOFF / WARMUP

        private bool m_PendingObjectToolHandoff;
        private AssetStampPrefab m_PendingHandoffStamp;
        private double m_PendingHandoffTimeoutAt;
 
        #endregion

        #region 5. RUNTIME STAMP

        protected AssetStampPrefab m_RuntimeStamp;
        public Entity RuntimeStampEntity { get; protected set; }
        protected bool m_RuntimeStampCreated;
        protected NetPrefab m_LastUsedRoadPrefab;

        #endregion

        #region 6. COST ENGINE STATE

        protected bool m_PendingCostResolve;
        protected int m_CostResolveRetries;
        protected AssetStampPrefab m_PendingCostStamp;
        protected ObjectSubNetInfo[] m_PendingCostSubNets;
        protected NetPrefab m_PendingCostRoadPrefab;
        protected float m_PendingCostHighestElevation;

        #endregion

        #region 7. SNAP STATE

        protected bool m_HasStoredSnapMask;
        protected Snap m_StoredSnapMask;

        protected bool m_SnapGeometryEnabled = true;
        protected bool m_SnapNetSideEnabled = false;
        protected bool m_SnapNetAreaEnabled = true;
        protected bool m_RoadSnapEnabled = true;

        #endregion

        #region 8. SESSION / EXIT / PLACEMENT

        protected ToolExitMode m_PendingExitMode = ToolExitMode.None;
        protected NetPrefab m_SessionRoadPrefab;
        protected NetPrefab m_PendingExitRoadPrefab;

        protected bool m_PendingDisableAfterPlacement;
        protected double m_PostPlaceDisableAt;
        protected double m_SuppressAutoDisableUntil;

        #endregion

        #region 9. INPUT

        private UnityEngine.InputSystem.InputAction m_CancelAction;
        protected float m_InputCooldown = 0f;

        #endregion   

        #region 10. ABSTRACT / VIRTUAL CONTRACTS

        protected abstract string GetToolName();
        protected abstract void ProcessToolInput();
        protected abstract bool TryMutateTargetStamp();

        protected virtual void OnToolActivated() { }
        protected virtual void OnToolDeactivated() { }
        protected virtual void OnToolTick() { }
        protected virtual void OnShapePlaced() { }

        #endregion
 

        #region 11. ECS LIFECYCLE

        /// <summary>Initializes system references and sets up input bindings when the system is created.</summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_NetToolSystem = World.GetOrCreateSystemManaged<NetToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolRaycastSystem = World.GetOrCreateSystemManaged<ToolRaycastSystem>();

            m_CancelAction = new UnityEngine.InputSystem.InputAction("MertsToolBoxCancel");
            m_CancelAction.AddBinding("<Keyboard>/escape");
            m_CancelAction.Enable();
        }

        /// <summary>Main update loop that processes tool transitions, inputs, shape generation, and rendering handoffs.</summary>
        protected override void OnUpdate()
        {
            if (m_PendingExitMode == ToolExitMode.RestoreFromEscape)
            {
                if (m_ToolSystem.activeTool != m_ObjectToolSystem)
                {
                    if (m_PendingExitRoadPrefab != null)
                    {
                        ForceUpdateUIAndTool(m_PendingExitRoadPrefab, updateTool: true, selectCategory: true);
                    }

                    m_PendingExitMode = ToolExitMode.None;
                    m_PendingExitRoadPrefab = null;
                    MertToolState.SuppressUiAbortDuringRestore = false;
                }
                return;
            }

            if (!ToolEnabled)
                return;

            if (HandleEscapeExit())
                return;

            if (HandleDeferredDisable())
                return;

            if (HandlePendingObjectToolHandoff())
                return;

            ProcessToolInput();

            if (m_PendingCreateShape)
            {
                m_PendingCreateShape = false;
                QueuePreviewRebuild();
            }

            UpdateQueuedPreviewRebuild();

            HandleExecuteCreateShape();
            HandlePendingCostResolve();

            if (RequiresSnapEnforcement)
            {
                EnforceRuntimeStampSnapMetadata();
            }

            OnToolTick();
        }

        /// <summary>Cleans up input actions and disposes of resources when the system is destroyed.</summary>
        protected override void OnDestroy()
        {
            m_CancelAction?.Disable();
            m_CancelAction?.Dispose();
            base.OnDestroy();
        }

        #endregion

        #region 12. TOOL STATE MANAGEMENT

        /// <summary>Activates or deactivates the tool mode, capturing current prefabs and snap states when enabling.</summary>
        public void SetToolState(bool isEnabled)
        {
            if (ToolEnabled == isEnabled)
                return;
         
            if (!isEnabled)
            {
                DisableToolMode(ToolExitMode.SilentTabClose);
                return;
            }

            var currentRoad = TryGetCurrentSelectedRoadPrefab();

            if (MertToolState.BlockRoadPrefabFallbackUntilNextRealSelection)
                m_SessionRoadPrefab = currentRoad;
            else
                m_SessionRoadPrefab = currentRoad ?? MertToolState.LastResolvedRoadPrefab;

            if (!m_HasStoredSnapMask && m_ToolSystem?.activeTool != null && m_ToolSystem.activeTool != m_ObjectToolSystem)
            {
                m_StoredSnapMask = m_ToolSystem.activeTool.selectedSnap;
                m_HasStoredSnapMask = true;
            }

            ToolEnabled = true;
       
            OnToolActivated();
            QueuePreviewRebuild();
        }

        /// <summary>Safely requests the tool to be disabled with a specified exit behavior mode.</summary>
        public void RequestDisable(ToolExitMode exitMode)
        {
            if (ToolEnabled)
                DisableToolMode(exitMode);
        }

        /// <summary>Deactivates the tool, resets internal live states, and restores the previous game UI and tool configuration.</summary>
        protected void DisableToolMode(ToolExitMode exitMode)
        {
            try
            {
                UnityEngine.Debug.LogWarning(
    $"[MertsToolBox][TOOL-STATE][{GetToolName()}] DisableToolMode({exitMode}) | ToolEnabled(before)={ToolEnabled} | activeTool={m_ToolSystem?.activeTool?.GetType().Name}");
                ToolEnabled = false;
                OnToolDeactivated();

                ClearLivePreviewState();
                RestoreSnapState();

                m_PendingDisableAfterPlacement = false;
                m_PendingCostResolve = false;

                if (exitMode == ToolExitMode.RestoreFromPlacement && m_SessionRoadPrefab != null)
                {
                    ForceUpdateUIAndTool(m_SessionRoadPrefab, updateTool: true, selectCategory: true);
                    m_PendingExitMode = ToolExitMode.None;
                }
                else if (exitMode == ToolExitMode.RestoreFromEscape && m_SessionRoadPrefab != null)
                {
                    MertToolState.SuppressUiAbortDuringRestore = true;
                    m_PendingExitMode = ToolExitMode.RestoreFromEscape;
                    m_PendingExitRoadPrefab = m_SessionRoadPrefab;
                }
                else if (exitMode == ToolExitMode.SilentTabClose && m_SessionRoadPrefab != null)
                {
                    ForceUpdateUIAndTool(m_SessionRoadPrefab, updateTool: false, selectCategory: false);
                    m_SessionRoadPrefab = null;
                    m_PendingExitMode = ToolExitMode.None;
                }
                else if (exitMode == ToolExitMode.UserSelectionClose)
                {
                    m_SessionRoadPrefab = null;
                    m_PendingExitMode = ToolExitMode.None;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] DisableToolMode error: {e.Message}");
            }
        }

        /// <summary>Resets pending handoffs, geometry sizes, and active tools to clear the visual preview from the map.</summary>
        private void ClearLivePreviewState()
        {
            ClearPendingHandoff();

            m_PreviewRebuildQueued = false;
            m_PendingCreateShape = false;
            m_ExecuteCreateShape = false;
            m_IsCreatingShape = false;


            if (RuntimeStampEntity != Entity.Null && EntityManager.Exists(RuntimeStampEntity))
            {
                if (EntityManager.TryGetComponent(RuntimeStampEntity, out ObjectGeometryData geom))
                {
                    geom.m_Size = float3.zero;
                    EntityManager.SetComponentData(RuntimeStampEntity, geom);
                }
            }

            if (m_ToolSystem != null && m_ObjectToolSystem != null && m_NetToolSystem != null)
            {
                if (m_ToolSystem.activeTool == m_ObjectToolSystem)
                {
                    m_ToolSystem.selected = Entity.Null;
                    m_ToolSystem.activeTool = m_NetToolSystem;
                }
            }
        }

        #endregion

        #region 13. SNAP STATE QUERIES

        /// <summary>Returns whether snapping to existing network geometry is currently enabled.</summary>
        public bool IsSnapGeometryEnabled() => m_SnapGeometryEnabled;

        /// <summary>Returns whether snapping to network sides is currently enabled.</summary>
        public bool IsSnapNetSideEnabled() => m_SnapNetSideEnabled;

        /// <summary>Returns whether snapping to network areas is currently enabled.</summary>
        public bool IsSnapNetAreaEnabled() => m_SnapNetAreaEnabled;

        #endregion

        #region 14. PREFAB / UI RESOLUTION

        /// <summary>Safely extracts the currently selected road prefab from the ToolbarUI binding or falls back to stored states.</summary>
        protected NetPrefab TryGetCurrentSelectedRoadPrefab()
        {
            try
            {
                var toolbarUISystem = World.GetExistingSystemManaged<Game.UI.InGame.ToolbarUISystem>();
                var prefabSystem = World.GetExistingSystemManaged<PrefabSystem>();

                if (toolbarUISystem != null && prefabSystem != null)
                {
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var field = toolbarUISystem.GetType().GetField("m_SelectedAssetBinding", flags);

                    if (field != null)
                    {
                        var binding = field.GetValue(toolbarUISystem);
                        if (binding != null)
                        {
                            var valueProp = binding.GetType().GetProperty("value");
                            if (valueProp != null)
                            {
                                var entityObj = valueProp.GetValue(binding);
                                if (entityObj is Entity entity && entity != Entity.Null)
                                {
                                    if (prefabSystem.TryGetPrefab<PrefabBase>(entity, out var prefabBase) && prefabBase is NetPrefab netPrefab)
                                    {
                                        return netPrefab;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[MertsToolBox] TryGetCurrentSelectedRoadPrefab error: {e.Message}");
            }

            return MertToolState.LastResolvedRoadPrefab;
        }

        /// <summary>Resolves the road prefab to be used by falling back through active, session, and globally stored preferences.</summary>
        protected NetPrefab ResolveRoadPrefabForToolWork()
        {
            var current = TryGetCurrentSelectedRoadPrefab();
            if (current != null) return current;
            if (m_SessionRoadPrefab != null) return m_SessionRoadPrefab;
            if (m_LastUsedRoadPrefab != null) return m_LastUsedRoadPrefab;
            if (MertToolState.LastResolvedRoadPrefab != null) return MertToolState.LastResolvedRoadPrefab;
            return null;
        }

        /// <summary>Forces the game UI and tool system to select a target prefab, suppressing memory captures to prevent infinite loops.</summary>
        private void ForceUpdateUIAndTool(NetPrefab targetPrefab, bool updateTool, bool selectCategory)
        {
            try
            {
                var toolbarUISystem = World.GetExistingSystemManaged<Game.UI.InGame.ToolbarUISystem>();
                Entity prefabEntity = m_PrefabSystem.GetEntity(targetPrefab);

                if (toolbarUISystem != null && prefabEntity != Entity.Null)
                {
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                    var selectMethod = toolbarUISystem.GetType().GetMethod("SelectAsset", flags, null, new Type[] { typeof(Entity), typeof(bool) }, null);
                    var selectCategoryMethod = toolbarUISystem.GetType().GetMethod("SelectAssetCategory", flags);

                    if (selectMethod != null)
                    {
                        MertToolState.SuppressUiMemoryCapture = true;
                        MertToolState.SuppressCategoryCapture = true;

                        try
                        {
                            if (selectCategory && selectCategoryMethod != null && MertToolState.LastResolvedCategory != Entity.Null)
                            {
                                selectCategoryMethod.Invoke(toolbarUISystem, new object[] { MertToolState.LastResolvedCategory });
                            }

                            selectMethod.Invoke(toolbarUISystem, new object[] { prefabEntity, updateTool });
                        }
                        finally
                        {
                            MertToolState.SuppressUiMemoryCapture = false;
                            MertToolState.SuppressCategoryCapture = false;
                        }
                        return;
                    }
                }

                if (updateTool)
                    m_ToolSystem.ActivatePrefabTool(targetPrefab);
            }
            catch (Exception)
            {
                MertToolState.SuppressUiMemoryCapture = false;
                MertToolState.SuppressCategoryCapture = false;

                if (updateTool)
                    m_ToolSystem.ActivatePrefabTool(targetPrefab);
            }
        }

        #endregion

        #region 15. UTILITIES

        /// <summary>Returns the previous index in an array, wrapping around to the end if it goes below zero.</summary>
        protected int CycleIndex<T>(int currentIndex, T[] steps)
        {
            if (steps == null || steps.Length == 0) return 0;
            int nextIndex = currentIndex - 1;
            return nextIndex < 0 ? steps.Length - 1 : nextIndex;
        }

        /// <summary>Retrieves a float value from a step array using a clamped index to prevent out-of-bounds exceptions.</summary>
        protected float GetCurrentStepValue(int currentIndex, float[] steps)
        {
            if (steps == null || steps.Length == 0) return 0f;
            return steps[math.clamp(currentIndex, 0, steps.Length - 1)];
        }

        /// <summary>Retrieves a generic value from a step array using a clamped index to prevent out-of-bounds exceptions.</summary>
        protected T GetCurrentStepValue<T>(int currentIndex, T[] steps)
        {
            if (steps == null || steps.Length == 0) return default;
            return steps[math.clamp(currentIndex, 0, steps.Length - 1)];
        }

        /// <summary>Calculates the next step-aligned float value based on the specified direction to ensure neat increments.</summary>
        protected static float GetNextStepAlignedValue(float currentValue, float stepSize, int direction)
        {
            if (stepSize <= 0f || direction == 0)
                return currentValue;

            const float epsilon = 0.0001f;

            if (direction > 0)
            {
                float next = math.floor(currentValue / stepSize) * stepSize + stepSize;
                if (next <= currentValue + epsilon)
                    next += stepSize;
                return next;
            }
            else
            {
                float prev = math.ceil(currentValue / stepSize) * stepSize - stepSize;
                if (prev >= currentValue - epsilon)
                    prev -= stepSize;
                return prev;
            }
        }

        /// <summary>Calculates the next step-aligned integer value based on the specified direction.</summary>
        protected static int GetNextStepAlignedInt(int currentValue, int stepSize, int direction)
        {
            if (stepSize <= 0 || direction == 0)
                return currentValue;

            if (direction > 0)
                return ((currentValue / stepSize) + 1) * stepSize;

            return ((currentValue - 1) / stepSize) * stepSize;
        }

        /// <summary>Calculates the next step-aligned float value and clamps it strictly within the provided minimum and maximum boundaries.</summary>
        protected static float GetNextStepAlignedValueClamped(float currentValue, float stepSize, int direction, float minValue, float maxValue)
        {
            float next = GetNextStepAlignedValue(currentValue, stepSize, direction);
            return math.clamp(next, minValue, maxValue);
        }

        /// <summary>Estimates the default physical width of the given road prefab from its geometry data, falling back to 8m.</summary>
        protected float EstimateRoadWidth(NetPrefab roadPrefab)
        {
            if (roadPrefab == null) return 8f;

            Entity entity = m_PrefabSystem.GetEntity(roadPrefab);
            if (EntityManager.TryGetComponent(entity, out NetGeometryData geometryData))
            {
                if (geometryData.m_DefaultWidth > 0.1f)
                    return geometryData.m_DefaultWidth;
            }

            return 8f;
        }

        /// <summary>Safely retrieves the current elevation level from the game's NetToolSystem, returning 0 on failure.</summary>
        protected float GetCurrentNetToolElevation()
        {
            try
            {
                return m_NetToolSystem == null ? 0f : m_NetToolSystem.elevation;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>Delays the automatic tool disable sequence to allow for continuous user interactions without interruptions.</summary>
        public void SuppressNextAutoDisable(double seconds = 0.08)
        {
            m_SuppressAutoDisableUntil = Math.Max(
                m_SuppressAutoDisableUntil,
                RealtimeNow + seconds
            );
        }

        #endregion
 
    }
}