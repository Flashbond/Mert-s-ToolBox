using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using MertsToolBox.Core;
using MertsToolBox.Management;
using MertsToolBox.Systems;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Unity.Entities;
using UnityEngine.Scripting;

namespace MertsToolBox
{
    public partial class MertToolBoxUISystem : UISystemBase
    {
        #region Constants & Fields
        private const string ModId = "MertsToolBox";
        private ToolSystem m_ToolSystem;

        private CircleToolSystem m_Circle;
        private HelixToolSystem m_Helix;
        private SuperEllipseToolSystem m_SuperEllipse;
        private GridToolSystem m_Grid;
        #endregion

        #region Nested Types
        /// <summary>
        /// A generic polling binding class used to synchronize backend values with the UI automatically.
        /// </summary>
        public class MertPolledBinding<T> : ValueBinding<T>, IUpdateBinding
        {
            private readonly Func<T> m_Getter;

            public MertPolledBinding(string group, string name, Func<T> getter, T initialValue = default)
                : base(group, name, initialValue)
            {
                m_Getter = getter;
            }
            public bool Update()
            {
                base.Update(m_Getter());
                return true;
            }
        }
        #endregion

        #region Lifecycle Methods
        /// <summary>
        /// Initializes system references, sets up event listeners, and registers UI bindings upon creation.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            m_Circle = World.GetOrCreateSystemManaged<CircleToolSystem>();
            m_Helix = World.GetOrCreateSystemManaged<HelixToolSystem>();
            m_SuperEllipse = World.GetOrCreateSystemManaged<SuperEllipseToolSystem>();
            m_Grid = World.GetOrCreateSystemManaged<GridToolSystem>();

            if (m_ToolSystem != null)
                m_ToolSystem.EventToolChanged += OnToolChanged;

            RegisterBindings();

            MertToolState.OnToolAbortedByUI += HandleToolAbortedByUi;
        }

        /// <summary>
        /// Continuously updates the global UI state and synchronizes individual tool bindings every frame.
        /// </summary>
        protected override void OnUpdate()
        {
            base.OnUpdate();

            MertToolState.HasReleasedStaleObjectToolThisFrame = false;

            TryReleaseStaleStampAfterReload();
            TryProcessPendingRestore();
        }

        /// <summary>
        /// Cleans up event listeners and system references to prevent memory leaks upon destruction.
        /// </summary>
        [Preserve]
        protected override void OnDestroy()
        {
            if (m_ToolSystem != null)
            {
                m_ToolSystem.EventToolChanged -= OnToolChanged;
            }

            MertToolState.OnToolAbortedByUI -= HandleToolAbortedByUi;

            base.OnDestroy();
        }
        #endregion

        #region UI Bindings Registration
        /// <summary>
        /// Registers all initial ValueBindings and TriggerBindings connecting the C# backend to the Cohtml/TSX frontend.
        /// </summary>
        private void RegisterBindings()
        {
            AddBinding(new ValueBinding<string>(
                ModId,
                "ToolList",
                GetToolListPipe()
            ));

            AddUpdateBinding(new MertPolledBinding<string>(ModId, "ActiveTool", () =>
            {
                if (m_Circle != null && m_Circle.ToolEnabled)
                    return $"{m_Circle.ToolId}|{m_Circle.ToolName}";

                if (m_Helix != null && m_Helix.ToolEnabled)
                    return $"{m_Helix.ToolId}|{m_Helix.ToolName}";

                if (m_SuperEllipse != null && m_SuperEllipse.ToolEnabled)
                    return $"{m_SuperEllipse.ToolId}|{m_SuperEllipse.ToolName}";

                if (m_Grid != null && m_Grid.ToolEnabled)
                    return $"{m_Grid.ToolId}|{m_Grid.ToolName}";

                return "None|None";
            }, "None|None"));

            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "IsToolBoxAllowed", () =>
            {
                bool prefabValid =
                    (m_Circle != null && m_Circle.IsCurrentPrefabValid()) ||
                    (m_Helix != null && m_Helix.IsCurrentPrefabValid()) ||
                    (m_SuperEllipse != null && m_SuperEllipse.IsCurrentPrefabValid()) ||
                    (m_Grid != null && m_Grid.IsCurrentPrefabValid());

                bool toolContextValid = m_ToolSystem != null && (
                    m_ToolSystem.activeTool is NetToolSystem ||
                    (m_Circle != null && m_Circle.ToolEnabled) ||
                    (m_Helix != null && m_Helix.ToolEnabled) ||
                    (m_SuperEllipse != null && m_SuperEllipse.ToolEnabled) ||
                    (m_Grid != null && m_Grid.ToolEnabled)
                );

                return prefabValid && toolContextValid;
            }, false));

            // Circle Bindings
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "CircleDiameter",
                () => m_Circle?.GetCurrentDiameter() ?? 96));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "CircleDiameterStepValue",
                () => m_Circle?.GetDiameterStepSize() ?? 8));
            AddBinding(new ValueBinding<int[]>(
                ModId,
                "CircleDiameterStepArray",
                m_Circle?.m_DiameterSteps ?? Array.Empty<int>(),
                new ArrayWriter<int>()
            ));

            // Helix Bindings
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "HelixDiameter",
                () => m_Helix?.GetCurrentDiameter() ?? 96));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "HelixDiameterStepValue",
                () => m_Helix?.GetDiameterStepSize() ?? 8));
            AddBinding(new ValueBinding<int[]>(
                ModId,
                "HelixDiameterStepArray",
                m_Helix?.m_DiameterSteps,
                new ArrayWriter<int>()
            ));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "HelixTurns",
                () => m_Helix?.GetCurrentTurns() ?? 3f));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "HelixTurnStepValue",
                () => m_Helix?.GetTurnStepSize() ?? 2));
            AddBinding(new ValueBinding<float[]>(
                ModId,
                "HelixTurnStepArray",
                m_Helix?.m_TurnSteps,
                new ArrayWriter<float>()
            ));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "HelixClearance",
                () => m_Helix?.GetCurrentClearance() ?? 8f));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "HelixClearanceStepValue",
                () => m_Helix?.GetClearanceStepSize() ?? 2));
            AddBinding(new ValueBinding<float[]>(
                ModId,
                "HelixClearanceStepArray",
                m_Helix?.m_ClearanceSteps,
                new ArrayWriter<float>()
            ));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "HelixIsClockwise",
                () => m_Helix?.GetIsClockwise() ?? true));

            // SuperEllipse Bindings
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "SuperEllipseWidth",
                () => m_SuperEllipse?.GetCurrentWidth() ?? 96));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "SuperEllipseWidthStepValue",
                () => m_SuperEllipse?.GetWidthStepSize() ?? 8));
            AddBinding(new ValueBinding<int[]>(
                ModId,
                "SuperEllipseWidthStepArray",
                m_SuperEllipse?.m_WidthSteps,
                new ArrayWriter<int>()
            ));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "SuperEllipseLength",
                () => m_SuperEllipse?.GetCurrentLength() ?? 192));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "SuperEllipseLengthStepValue",
                () => m_SuperEllipse?.GetLengthStepSize() ?? 8));
            AddBinding(new ValueBinding<int[]>(
                ModId,
                "SuperEllipseLengthStepArray",
                m_SuperEllipse?.m_LengthSteps,
                new ArrayWriter<int>()
            ));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "SuperEllipseN",
                         () => m_SuperEllipse?.GetCurrentNSliderValue() ?? 8f));

            // Grid Bindings
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "GridBlockWidth",
                () => m_Grid?.GetCurrentBlockWidthU() ?? 12));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "GridBlockLength",
                () => m_Grid?.GetCurrentBlockLengthU() ?? 12));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "GridColumns",
                () => m_Grid?.GetCurrentColumns() ?? 2));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "GridRows",
                () => m_Grid?.GetCurrentRows() ?? 2));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "GridAlternating",
                () => m_Grid?.GetIsAlternating() ?? false));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "GridOrientationLeftBottom",
                () => m_Grid?.GetIsOrientationLeftBottom() ?? false));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "GridIsOneWaySupported",
                () => m_Grid?.IsCurrentPrefabValidForOneWayPattern() ?? false));

            // Elevation Bindings
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "ElevationStepValue", () =>
            {
                if (m_Circle != null && m_Circle.ToolEnabled) return m_Circle.GetElevationStepValue();
                if (m_Helix != null && m_Helix.ToolEnabled) return m_Helix.GetElevationStepValue();
                if (m_SuperEllipse != null && m_SuperEllipse.ToolEnabled) return m_SuperEllipse.GetElevationStepValue();
                if (m_Grid != null && m_Grid.ToolEnabled) return m_Grid.GetElevationStepValue();

                return 10f;
            }));

            AddBinding(new ValueBinding<float[]>(
                ModId,
                "ElevationStepArray",
                m_Circle?.GetElevationStepArray() ?? new float[] { 10f, 5f, 2.5f, 1.25f },
                new ArrayWriter<float>()
            ));

            AddUpdateBinding(new MertPolledBinding<float>(ModId, "ElevationValue", () =>
            {
                if (m_Circle != null && m_Circle.ToolEnabled) return m_Circle.GetCurrentNetToolElevation();
                if (m_SuperEllipse != null && m_SuperEllipse.ToolEnabled) return m_SuperEllipse.GetCurrentNetToolElevation();
                if (m_Grid != null && m_Grid.ToolEnabled) return m_Grid.GetCurrentNetToolElevation();

                return 0f;
            }));

            // Shared Snap & Toggle Bindings
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "ShowSnapRow", () =>
            {
                if (m_Grid != null && m_Grid.ToolEnabled)
                    return Mod.settings != null && Mod.settings.EnableGridSnap;
                return true;
            }));

            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "IsSnapGeometryActive", () =>
            {
                if (m_Circle != null && m_Circle.ToolEnabled) return m_Circle.IsSnapGeometryEnabled();
                if (m_SuperEllipse != null && m_SuperEllipse.ToolEnabled) return m_SuperEllipse.IsSnapGeometryEnabled();
                if (m_Grid != null && m_Grid.ToolEnabled) return m_Grid.IsSnapGeometryEnabled();
                return false;
            }));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "IsSnapNetSideActive", () =>
            {
                if (m_Circle != null && m_Circle.ToolEnabled) return m_Circle.IsSnapNetSideEnabled();
                if (m_SuperEllipse != null && m_SuperEllipse.ToolEnabled) return m_SuperEllipse.IsSnapNetSideEnabled();
                if (m_Grid != null && m_Grid.ToolEnabled) return m_Grid.IsSnapNetSideEnabled();
                return false;
            }));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "IsSnapNetAreaActive", () =>
            {
                if (m_Circle != null && m_Circle.ToolEnabled) return m_Circle.IsSnapNetAreaEnabled();
                if (m_SuperEllipse != null && m_SuperEllipse.ToolEnabled) return m_SuperEllipse.IsSnapNetAreaEnabled();
                if (m_Grid != null && m_Grid.ToolEnabled) return m_Grid.IsSnapNetAreaEnabled();
                return false;
            }));

            // Action Hints
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "ShowCircleCtrlWheelHint",
                () => Mod.settings != null && Mod.settings.UseCtrlWheelForCircleDiameterAdjustment));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "ShowHelixCtrlWheelHint",
                () => Mod.settings != null && Mod.settings.UseCtrlWheelForHelixTurnAdjustment));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "ShowSuperEllipseCtrlWheelHint",
                () => Mod.settings != null && Mod.settings.UseCtrlWheelForShapeAdjustment));
            AddUpdateBinding(new MertPolledBinding<string>(ModId, "ActionStatusText",
                () => GetActionStatusText(), ""));

            // Global Triggers
            AddBinding(new TriggerBinding<string>(ModId, "ToggleTool", (toolId) =>
            {
                MertToolState.UserJustChangedAssetCategory = false;
                CloseTools(ToolExitMode.UserSelectionClose);

                if (toolId == m_Circle?.ToolId) m_Circle.SetToolState(true);
                else if (toolId == m_Helix?.ToolId) m_Helix.SetToolState(true);
                else if (toolId == m_SuperEllipse?.ToolId) m_SuperEllipse.SetToolState(true);
                else if (toolId == m_Grid?.ToolId) m_Grid.SetToolState(true);
            }));

            AddBinding(new TriggerBinding<float>(ModId, "ElevationStep", (val) =>
            {
                if (m_Circle != null && m_Circle.ToolEnabled) m_Circle.SetElevationStepFromUi(val);
                else if (m_Helix != null && m_Helix.ToolEnabled) m_Helix.SetElevationStepFromUi(val);
                else if (m_SuperEllipse != null && m_SuperEllipse.ToolEnabled) m_SuperEllipse.SetElevationStepFromUi(val);
                else if (m_Grid != null && m_Grid.ToolEnabled) m_Grid.SetElevationStepFromUi(val);
            }));

            AddBinding(new TriggerBinding(ModId, "ElevationUp", () =>
            {
                if (m_Circle != null && m_Circle.ToolEnabled) m_Circle.QueueElevationChangeFromUi(+1);
                else if (m_SuperEllipse != null && m_SuperEllipse.ToolEnabled) m_SuperEllipse.QueueElevationChangeFromUi(+1);
                else if (m_Grid != null && m_Grid.ToolEnabled) m_Grid.QueueElevationChangeFromUi(+1);
            }));

            AddBinding(new TriggerBinding(ModId, "ElevationDown", () =>
            {
                if (m_Circle != null && m_Circle.ToolEnabled) m_Circle.QueueElevationChangeFromUi(-1);
                else if (m_SuperEllipse != null && m_SuperEllipse.ToolEnabled) m_SuperEllipse.QueueElevationChangeFromUi(-1);
                else if (m_Grid != null && m_Grid.ToolEnabled) m_Grid.QueueElevationChangeFromUi(-1);
            }));

            // Circle Triggers
            AddBinding(new TriggerBinding(ModId, "CircleDiameterUp", () => m_Circle?.QueueDiameterChange(+1)));
            AddBinding(new TriggerBinding(ModId, "CircleDiameterDown", () => m_Circle?.QueueDiameterChange(-1)));
            AddBinding(new TriggerBinding<int>(ModId, "CircleDiameterStep", (val) => m_Circle?.QueueSetDiameterStep(val)));
            AddBinding(new TriggerBinding<string>(ModId, "ToggleCircleSnap", (snapType) => m_Circle?.QueueSnapToggle(snapType)));

            // Helix Triggers
            AddBinding(new TriggerBinding(ModId, "HelixDiameterUp", () => m_Helix?.QueueDiameterChange(+1)));
            AddBinding(new TriggerBinding(ModId, "HelixDiameterDown", () => m_Helix?.QueueDiameterChange(-1)));
            AddBinding(new TriggerBinding<int>(ModId, "HelixDiameterStep", (val) => m_Helix?.QueueSetDiameterStep(val)));
            AddBinding(new TriggerBinding(ModId, "HelixTurnsUp", () => m_Helix?.QueueTurnChange(+1)));
            AddBinding(new TriggerBinding(ModId, "HelixTurnsDown", () => m_Helix?.QueueTurnChange(-1)));
            AddBinding(new TriggerBinding<float>(ModId, "HelixTurnsStep", (val) => m_Helix?.QueueSetTurnStep(val)));
            AddBinding(new TriggerBinding(ModId, "HelixClearanceUp", () => m_Helix?.QueueClearanceChange(+1)));
            AddBinding(new TriggerBinding(ModId, "HelixClearanceDown", () => m_Helix?.QueueClearanceChange(-1)));
            AddBinding(new TriggerBinding<float>(ModId, "HelixClearanceStep", (val) => m_Helix?.QueueSetClearanceStep(val)));
            AddBinding(new TriggerBinding(ModId, "HelixToggleDirection", () => m_Helix?.QueueToggleDirection()));

            // SuperEllipse Triggers
            AddBinding(new TriggerBinding(ModId, "SuperEllipseWidthUp", () => m_SuperEllipse?.QueueWidthChange(+1)));
            AddBinding(new TriggerBinding(ModId, "SuperEllipseWidthDown", () => m_SuperEllipse?.QueueWidthChange(-1)));
            AddBinding(new TriggerBinding<int>(ModId, "SuperEllipseWidthStep", (val) => m_SuperEllipse?.QueueSetWidthStep(val)));
            AddBinding(new TriggerBinding(ModId, "SuperEllipseLengthUp", () => m_SuperEllipse?.QueueLengthChange(+1)));
            AddBinding(new TriggerBinding(ModId, "SuperEllipseLengthDown", () => m_SuperEllipse?.QueueLengthChange(-1)));
            AddBinding(new TriggerBinding<int>(ModId, "SuperEllipseLengthStep", (val) => m_SuperEllipse?.QueueSetLengthStep(val)));
            AddBinding(new TriggerBinding<float>(ModId, "SuperEllipseSetN", (value) => m_SuperEllipse?.SetNFromUi(value)));
            AddBinding(new TriggerBinding<string>(ModId, "SuperEllipseToggleSnap", (snapType) => m_SuperEllipse?.QueueSnapToggle(snapType)));

            // Grid Triggers
            AddBinding(new TriggerBinding(ModId, "GridBlockWidthUp", () => m_Grid?.QueueBlockWidthChange(+1)));
            AddBinding(new TriggerBinding(ModId, "GridBlockWidthDown", () => m_Grid?.QueueBlockWidthChange(-1)));
            AddBinding(new TriggerBinding(ModId, "GridBlockLengthUp", () => m_Grid?.QueueBlockLengthChange(+1)));
            AddBinding(new TriggerBinding(ModId, "GridBlockLengthDown", () => m_Grid?.QueueBlockLengthChange(-1)));
            AddBinding(new TriggerBinding(ModId, "GridColumnsUp", () => m_Grid?.QueueColsChange(+1)));
            AddBinding(new TriggerBinding(ModId, "GridColumnsDown", () => m_Grid?.QueueColsChange(-1)));
            AddBinding(new TriggerBinding(ModId, "GridRowsUp", () => m_Grid?.QueueRowsChange(+1)));
            AddBinding(new TriggerBinding(ModId, "GridRowsDown", () => m_Grid?.QueueRowsChange(-1)));
            AddBinding(new TriggerBinding(ModId, "GridToggleAlternating", () => m_Grid?.QueueToggleAlternating()));
            AddBinding(new TriggerBinding(ModId, "GridToggleOrientation", () => m_Grid?.QueueToggleOrientation()));
            AddBinding(new TriggerBinding<string>(ModId, "GridToggleSnap", (snapType) => m_Grid?.QueueSnapToggle(snapType)));
        }
        #endregion

        #region Tool Event Handling
        /// <summary>
        /// Handles forceful tool abort requests originating from the UI system.
        /// </summary>
        private void HandleToolAbortedByUi(ToolExitMode exitMode)
        {
            if (MertToolState.SuppressUiAbortDuringRestore) return;
            CloseTools(exitMode);
        }

        /// <summary>
        /// Disables all custom tools securely based on the provided exit mode.
        /// </summary>
        private void CloseTools(ToolExitMode exitMode)
        {
            m_Circle?.RequestDisable(exitMode);
            m_Helix?.RequestDisable(exitMode);
            m_SuperEllipse?.RequestDisable(exitMode);
            m_Grid?.RequestDisable(exitMode);
        }

        /// <summary>
        /// Listens to global tool changes and safely closes custom tools if standard game tools take over.
        /// </summary>
        private void OnToolChanged(ToolBaseSystem tool)
        {
            if (MertToolState.SuppressToolChangedDuringColdstart)
                return;
            if (MertToolState.SuppressUiAbortDuringRestore)
                return;

            var objectTool = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            var netTool = World.GetOrCreateSystemManaged<NetToolSystem>();
            var defaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            bool isRoadBuilderTool = IsRoadBuilderTool(tool);

            if (isRoadBuilderTool)
            {
                CloseTools(ToolExitMode.SilentTabClose);
                return;
            }
            if (tool == defaultTool)
            {
                if (MertToolState.UserJustChangedAssetCategory)
                    return;

                CloseTools(ToolExitMode.RestoreFromEscape);
                return;
            }

            if (tool == netTool)
            {
                if (MertToolState.UserJustChangedAssetCategory)
                {
                    MertToolState.UserJustChangedAssetCategory = false;
                    return;
                }
                return;
            }

            if (tool != objectTool && tool != netTool)
            {
                if (TryClassifyExternalToolChange(out ToolExitMode externalExitMode))
                {
                    CloseTools(externalExitMode);
                    return;
                }

                CloseTools(ToolExitMode.UserSelectionClose);
            }
        }
        #endregion

        #region External Selection Normalization
        /// <summary>
        /// Determines whether an asset is currently selected through the standard vanilla toolbar UI.
        /// </summary>
        private bool HasVanillaSelectedAsset()
        {
            try
            {
                var toolbarSystem = World.GetExistingSystemManaged<Game.UI.InGame.ToolbarUISystem>();
                if (toolbarSystem == null)
                    return false;

                var bindingField = typeof(Game.UI.InGame.ToolbarUISystem)
                    .GetField("m_SelectedAssetBinding", BindingFlags.Instance | BindingFlags.NonPublic);

                if (bindingField?.GetValue(toolbarSystem) is not object bindingObj)
                    return false;

                var valueProperty = bindingObj.GetType().GetProperty("value");
                if (valueProperty == null)
                    return false;

                Entity selectedAssetEntity = (Entity)valueProperty.GetValue(bindingObj);
                return selectedAssetEntity != Entity.Null;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// Evaluates external prefab selections to determine the appropriate tool exit mode based on category synchronization.
        /// </summary>
        private bool TryClassifyExternalToolChange(out ToolExitMode exitMode)
        {
            exitMode = ToolExitMode.UserSelectionClose;

            if (HasVanillaSelectedAsset())
                return false;

            var netTool = World.GetExistingSystemManaged<NetToolSystem>();
            var prefabSystem = World.GetExistingSystemManaged<PrefabSystem>();

            if (netTool == null || prefabSystem == null)
                return false;

            if (netTool.GetPrefab() is not NetPrefab currentRoad || currentRoad == null)
                return false;

            Entity roadEntity = prefabSystem.GetEntity(currentRoad);
            if (roadEntity == Entity.Null || !World.EntityManager.Exists(roadEntity))
                return false;

            if (!MertToolbarHandoffMemory.IsRoadNetPrefab(roadEntity, out var resolvedRoad) || resolvedRoad == null)
                return false;

            Entity newCategory = Entity.Null;
            if (World.EntityManager.TryGetComponent(roadEntity, out UIObjectData uiObject) &&
                uiObject.m_Group != Entity.Null)
            {
                newCategory = uiObject.m_Group;
            }

            Entity baselineCategory = MertToolState.LastResolvedCategory != Entity.Null
                ? MertToolState.LastResolvedCategory
                : MertToolState.LaunchCategory;

            bool sameCategory =
                newCategory != Entity.Null &&
                baselineCategory != Entity.Null &&
                newCategory == baselineCategory;

            exitMode = sameCategory
                ? ToolExitMode.UserSelectionClose
                : ToolExitMode.SilentTabClose;

            return true;
        }

        /// <summary>
        /// Checks whether the specified tool system corresponds to the external Road Builder mod.
        /// </summary>
        public static bool IsRoadBuilderTool(ToolBaseSystem tool)
        {
            if (tool == null)
                return false;

            string typeName = tool.GetType().Name;
            return string.Equals(typeName, "RoadBuilderToolSystem", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Stale Stamp Recovery
        /// <summary>
        /// Recovers the tool state by forcing a silent exit if the active object tool is stuck holding a destroyed or invalid stamp entity.
        /// </summary>
        private void TryReleaseStaleStampAfterReload()
        {
            if (m_ToolSystem == null)
                return;

            if (MertToolState.HasReleasedStaleObjectToolThisFrame)
                return;

            if (MertToolState.PendingRestore)
                return;

            var objectTool = World.GetExistingSystemManaged<ObjectToolSystem>();
            if (objectTool == null)
                return;

            if (m_ToolSystem.activeTool != objectTool)
                return;

            PrefabBase currentPrefab = null;
            try
            {
                currentPrefab = objectTool.GetPrefab();
            }
            catch
            {
                return;
            }

            if (currentPrefab == null)
                return;

            if (!MertToolbarHandoffMemory.IsCurrentStamp(currentPrefab))
                return;

            var prefabSystem = World.GetExistingSystemManaged<PrefabSystem>();
            if (prefabSystem == null)
                return;

            Entity stampEntity = prefabSystem.GetEntity(currentPrefab);
            bool entityAlive = stampEntity != Entity.Null && World.EntityManager.Exists(stampEntity);

            if (!entityAlive)
            {
                MertToolState.HasReleasedStaleObjectToolThisFrame = true;
                ModRuntime.Log("TryReleaseStaleStampAfterReload | stale stamp detected -> SilentTabClose");
                CloseTools(ToolExitMode.SilentTabClose);
            }
        }

        #endregion

        #region Handoff & Restore
        /// <summary>
        /// Processes any pending restore operations to re-establish previous tool states and selections.
        /// </summary>
        private void TryProcessPendingRestore()
        {
            if (!MertToolState.PendingRestore)
                return;

            if (MertToolState.PendingRestoreMode == ToolExitMode.None)
            {
                MertToolState.ClearPendingRestore();
                return;
            }

            if (m_ToolSystem == null)
                return;

            NetPrefab road = MertToolState.PendingRestoreRoad;
            Entity category = MertToolState.PendingRestoreCategory;

            var toolbarUISystem = World.GetExistingSystemManaged<Game.UI.InGame.ToolbarUISystem>();
            var prefabSystem = World.GetExistingSystemManaged<PrefabSystem>();
            var netTool = World.GetOrCreateSystemManaged<NetToolSystem>();

            if (toolbarUISystem == null || prefabSystem == null || netTool == null)
                return;

            MertToolState.SuppressUiAbortDuringRestore = true;
            MertToolState.SuppressLiveUiCapture = true;
            MertToolState.SuppressUiMemoryCapture = true;
            MertToolState.SuppressCategoryCapture = true;

            try
            {
                var flags = System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Public;

                var selectCategory = toolbarUISystem.GetType()
                    .GetMethod("SelectAssetCategory", flags, null, new Type[] { typeof(Entity) }, null);

                var selectAsset = toolbarUISystem.GetType()
                    .GetMethod("SelectAsset", flags, null, new Type[] { typeof(Entity), typeof(bool) }, null);

                bool categoryLooksValid =
                    category != Entity.Null &&
                    World != null &&
                    World.EntityManager.Exists(category) &&
                    World.EntityManager.HasComponent<UIAssetCategoryData>(category);

                if (!categoryLooksValid && road != null)
                {
                    Entity roadEntity = prefabSystem.GetEntity(road);
                    if (roadEntity != Entity.Null &&
                        World.EntityManager.Exists(roadEntity) &&
                        World.EntityManager.TryGetComponent(roadEntity, out UIObjectData uiObject) &&
                        uiObject.m_Group != Entity.Null &&
                        World.EntityManager.Exists(uiObject.m_Group) &&
                        World.EntityManager.HasComponent<UIAssetCategoryData>(uiObject.m_Group))
                    {
                        category = uiObject.m_Group;
                        categoryLooksValid = true;
                    }
                }

                if (categoryLooksValid)
                {
                    try
                    {
                        selectCategory?.Invoke(toolbarUISystem, new object[] { category });
                    }
                    catch (TargetInvocationException tie)
                    {
                        ModRuntime.Warn(
                            $"Restore category failed: {tie.InnerException?.Message ?? tie.Message}");
                    }
                    catch (Exception e)
                    {
                        ModRuntime.Warn(
                            $"Restore category failed: {e.Message}");
                    }
                }

                if (road != null)
                {
                    Entity roadEntity = prefabSystem.GetEntity(road);
                    if (roadEntity != Entity.Null && World.EntityManager.Exists(roadEntity))
                    {
                        try
                        {
                            selectAsset?.Invoke(toolbarUISystem, new object[] { roadEntity, true });
                        }
                        catch (TargetInvocationException tie)
                        {
                            ModRuntime.Warn(
                                $"Restore asset failed: {tie.InnerException?.Message ?? tie.Message}");
                        }
                        catch (Exception e)
                        {
                            ModRuntime.Warn($"Restore asset failed: {e.Message}");
                        }
                    }
                }

                m_ToolSystem.activeTool = netTool;
                string activeAfter =
                    m_ToolSystem?.activeTool == null ? "NULL" :
                    m_ToolSystem.activeTool.GetType().Name;

                if (road != null)
                {
                    try
                    {
                        m_ToolSystem.ActivatePrefabTool(road);
                    }
                    catch (Exception e)
                    {
                        ModRuntime.Warn($"ActivatePrefabTool failed during restore: {e.Message}");
                    }
                }
            }
            finally
            {
                MertToolState.SuppressCategoryCapture = false;
                MertToolState.SuppressUiMemoryCapture = false;
                MertToolState.SuppressLiveUiCapture = false;
                MertToolState.SuppressUiAbortDuringRestore = false;
                MertToolState.ClearPendingRestore();
            }
        }
        #endregion

        #region Helpers & UI Formatting Utilities
        private string GetToolListPipe()
        {
            var parts = new List<string>();

            if (m_Circle != null)
                parts.Add($"{m_Circle.ToolId}|{m_Circle.ToolName}|{m_Circle.ToolId}");

            if (m_Helix != null)
                parts.Add($"{m_Helix.ToolId}|{m_Helix.ToolName}|{m_Helix.ToolId}");

            if (m_SuperEllipse != null)
                parts.Add($"{m_SuperEllipse.ToolId}|{m_SuperEllipse.ToolName}|{m_SuperEllipse.ToolId}");

            if (m_Grid != null)
                parts.Add($"{m_Grid.ToolId}|{m_Grid.ToolName}|{m_Grid.ToolId}");

            return string.Join(";", parts);
        }
        /// <summary>
        /// Generates the formatted status text displaying current metrics for the active tool.
        /// </summary>
        private string GetActionStatusText()
        {
            if (m_Circle != null && m_Circle.ToolEnabled)
            {
                CircleMetrics m = m_Circle.GetCurrentCircleMetrics();
                return $"Outer: {FormatSmart(m.OuterDiameterUnits)}U ({FormatSmart(m.OuterDiameterMeters)}m) - " +
                       $"Inner: {FormatSmart(m.InnerDiameterUnits)}U ({FormatSmart(m.InnerDiameterMeters)}m)";
            }
            if (m_Helix != null && m_Helix.ToolEnabled)
            {
                return $"Diameter: {FormatSmart(m_Helix.GetCurrentDiameter())}m - " +
                       $"Turns: {FormatSmart(m_Helix.GetCurrentTurns())} - " +
                       $"Clearance: {FormatSmart(m_Helix.GetCurrentClearance())}m";
            }
            if (m_SuperEllipse != null && m_SuperEllipse.ToolEnabled)
            {
                return $"Width: {FormatSmart(m_SuperEllipse.GetCurrentWidth())}m - " +
                       $"Length: {FormatSmart(m_SuperEllipse.GetCurrentLength())}m - " +
                       $"N: {FormatSmart(m_SuperEllipse.GetCurrentNSliderValue())}";
            }
            if (m_Grid != null && m_Grid.ToolEnabled)
            {
                return $"Width: {FormatSmart(m_Grid.GetCurrentBlockWidthU())}U - " +
                       $"Length: {FormatSmart(m_Grid.GetCurrentBlockLengthU())}U - " +
                       $"Rows: {FormatSmart(m_Grid.GetCurrentRows())} - " +
                       $"Columns: {FormatSmart(m_Grid.GetCurrentColumns())}";
            }
            return string.Empty;
        }

        /// <summary>
        /// Formats a floating-point value to a concise string representation.
        /// </summary>
        private static string FormatSmart(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats an integer value to a string representation.
        /// </summary>
        private static string FormatSmart(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
        #endregion
    }
}