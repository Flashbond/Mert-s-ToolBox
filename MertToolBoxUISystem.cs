using Colossal.UI.Binding;
using Game.Tools;
using Game.UI;
using System;
using Unity.Entities;
using UnityEngine.Scripting;
using System.Globalization;

namespace MertsToolBox
{
    public partial class MertToolBoxUISystem : UISystemBase
    {
        #region 1. CONSTANTS & SYSTEM REFERENCES

        private const string ModId = "MertsToolBox";
        private ToolSystem m_ToolSystem;

        private CircleToolSystem m_CircleTool;
        private HelixToolSystem m_HelixTool;
        private SuperEllipseToolSystem m_SuperEllipseTool;
        private GridToolSystem m_GridTool;

        #endregion

        #region 2. GLOBAL UI STATE BINDINGS

        public static DateTime m_LastUiInteractionTime = DateTime.MinValue;
        private ValueBinding<string> m_ActiveToolBinding;
        private ValueBinding<bool> m_IsToolBoxAllowedBinding;

        #endregion

        #region 3. SHARED SNAP & TOGGLE BINDINGS

        private ValueBinding<bool> m_IsSnapGeometryActiveBinding;
        private ValueBinding<bool> m_IsSnapNetSideActiveBinding;
        private ValueBinding<bool> m_IsSnapNetAreaActiveBinding;
        private ValueBinding<bool> m_IsSubtractActiveBinding;

        #endregion

        #region 4. TOOL-SPECIFIC BINDINGS

        // Circle
        private ValueBinding<float> m_CircleDiameterBinding;
        private ValueBinding<int> m_CircleDiameterStepIndexBinding;
        private ValueBinding<int> m_CircleDiameterStepSizeBinding;

        // Helix
        private ValueBinding<float> m_HelixDiameterBinding;
        private ValueBinding<int> m_HelixDiameterStepIndexBinding;
        private ValueBinding<float> m_HelixTurnsBinding;
        private ValueBinding<int> m_HelixTurnStepIndexBinding;
        private ValueBinding<float> m_HelixClearanceBinding;
        private ValueBinding<int> m_HelixClearanceStepIndexBinding;

        // SuperEllipse
        private ValueBinding<float> m_SuperEllipseWidthBinding;
        private ValueBinding<int> m_SuperEllipseWidthStepIndexBinding;
        private ValueBinding<float> m_SuperEllipseLengthBinding;
        private ValueBinding<int> m_SuperEllipseLengthStepIndexBinding;
        private ValueBinding<float> m_SuperEllipseNBinding;

        // Grid
        private ValueBinding<int> m_GridBlockWidthBinding;
        private ValueBinding<int> m_GridBlockLengthBinding;
        private ValueBinding<int> m_GridColumnsBinding;
        private ValueBinding<int> m_GridRowsBinding;
        private ValueBinding<bool> m_GridAlternatingBinding;
        private ValueBinding<bool> m_GridOrientationLeftBottomBinding;
        private ValueBinding<bool> m_GridIsOneWaySupportedBinding;

        // Hints
        private ValueBinding<bool> m_ShowCircleCtrlWheelHintBinding;
        private ValueBinding<bool> m_ShowHelixCtrlWheelHintBinding;
        private ValueBinding<bool> m_ShowSuperEllipseCtrlWheelHintBinding;
        private ValueBinding<string> m_ActionStatusTextBinding;

        #endregion

        #region 5. ECS LIFECYCLE

        /// <summary>
        /// Initializes system references, sets up event listeners, and registers UI bindings upon creation.
        /// </summary>
        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_CircleTool = World.GetOrCreateSystemManaged<CircleToolSystem>();
            m_HelixTool = World.GetOrCreateSystemManaged<HelixToolSystem>();
            m_SuperEllipseTool = World.GetOrCreateSystemManaged<SuperEllipseToolSystem>();
            m_GridTool = World.GetOrCreateSystemManaged<GridToolSystem>();

            if (m_ToolSystem != null) m_ToolSystem.EventToolChanged += OnToolChanged;

            RegisterBindings();
            MertToolState.OnToolAbortedByUI += HandleToolAbortedByUi;

        }

        /// <summary>
        /// Continuously updates the global UI state and synchronizes individual tool bindings every frame.
        /// </summary>
        protected override void OnUpdate()
        {
            base.OnUpdate();

            UpdateToolBoxAllowedState();
            MertToolState.HasReleasedStaleObjectToolThisFrame = false;

            UpdateActiveToolState();
            UpdateCircleBindings();
            UpdateHelixBindings();
            UpdateSuperEllipseBindings();
            UpdateGridBindings();
            UpdateSharedSnapBindings();
            UpdateSharedSubtractBinding();
            UpdateActionHintBindings();
        }

        /// <summary>
        /// Cleans up event listeners to prevent memory leaks when the system is destroyed.
        /// </summary>
        [Preserve]
        protected override void OnDestroy()
        {
            if (m_ToolSystem != null) m_ToolSystem.EventToolChanged -= OnToolChanged;
            MertToolState.OnToolAbortedByUI -= HandleToolAbortedByUi;

            base.OnDestroy();
        }

        #endregion

        #region 6. BINDING REGISTRATION

        /// <summary>
        /// Registers all initial ValueBindings and TriggerBindings connecting the C# backend to the Cohtml/TSX frontend.
        /// </summary>
        private void RegisterBindings()
        {
            // Global State
            AddBinding(m_ActiveToolBinding = new ValueBinding<string>(ModId, "ActiveTool", "None"));
            AddBinding(m_IsToolBoxAllowedBinding = new ValueBinding<bool>(ModId, "IsToolBoxAllowed", false));

            // Circle Bindings
            AddBinding(m_CircleDiameterBinding = new ValueBinding<float>(ModId, "CircleDiameter", 80f));
            AddBinding(m_CircleDiameterStepIndexBinding = new ValueBinding<int>(ModId, "CircleDiameterStepIndex", 3));
            AddBinding(m_CircleDiameterStepSizeBinding = new ValueBinding<int>(ModId, "CircleDiameterStepSize", 8));

            // Helix Bindings
            AddBinding(m_HelixDiameterBinding = new ValueBinding<float>(ModId, "HelixDiameter", 80f));
            AddBinding(m_HelixDiameterStepIndexBinding = new ValueBinding<int>(ModId, "HelixDiameterStepIndex", 0));
            AddBinding(m_HelixTurnsBinding = new ValueBinding<float>(ModId, "HelixTurns", 3f));
            AddBinding(m_HelixTurnStepIndexBinding = new ValueBinding<int>(ModId, "HelixTurnStepIndex", 0));
            AddBinding(m_HelixClearanceBinding = new ValueBinding<float>(ModId, "HelixClearance", 8f));
            AddBinding(m_HelixClearanceStepIndexBinding = new ValueBinding<int>(ModId, "HelixClearanceStepIndex", 0));

            // SuperEllipse Bindings
            AddBinding(m_SuperEllipseWidthBinding = new ValueBinding<float>(ModId, "SuperEllipseWidth", 80f));
            AddBinding(m_SuperEllipseWidthStepIndexBinding = new ValueBinding<int>(ModId, "SuperEllipseWidthStepIndex", 0));
            AddBinding(m_SuperEllipseLengthBinding = new ValueBinding<float>(ModId, "SuperEllipseLength", 160f));
            AddBinding(m_SuperEllipseLengthStepIndexBinding = new ValueBinding<int>(ModId, "SuperEllipseLengthStepIndex", 0));
            AddBinding(m_SuperEllipseNBinding = new ValueBinding<float>(ModId, "SuperEllipseN", 8f));

            // Grid Bindings
            AddBinding(m_GridBlockWidthBinding = new ValueBinding<int>(ModId, "GridBlockWidth", 12));
            AddBinding(m_GridBlockLengthBinding = new ValueBinding<int>(ModId, "GridBlockLength", 12));
            AddBinding(m_GridColumnsBinding = new ValueBinding<int>(ModId, "GridColumns", 2));
            AddBinding(m_GridRowsBinding = new ValueBinding<int>(ModId, "GridRows", 2));
            AddBinding(m_GridAlternatingBinding = new ValueBinding<bool>(ModId, "GridAlternating", false));
            AddBinding(m_GridOrientationLeftBottomBinding = new ValueBinding<bool>(ModId, "GridOrientationLeftBottom", false));
            AddBinding(m_GridIsOneWaySupportedBinding = new ValueBinding<bool>(ModId, "GridIsOneWaySupported", false));

            // Shared Snap & Toggle Bindings
            AddBinding(m_IsSnapGeometryActiveBinding = new ValueBinding<bool>(ModId, "IsSnapGeometryActive", false));
            AddBinding(m_IsSnapNetSideActiveBinding = new ValueBinding<bool>(ModId, "IsSnapNetSideActive", false));
            AddBinding(m_IsSnapNetAreaActiveBinding = new ValueBinding<bool>(ModId, "IsSnapNetAreaActive", false));
            AddBinding(m_IsSubtractActiveBinding = new ValueBinding<bool>(ModId, "IsSubtractActive", false));

            // Action Hints
            AddBinding(m_ShowCircleCtrlWheelHintBinding = new ValueBinding<bool>(ModId, "ShowCircleCtrlWheelHint", false));
            AddBinding(m_ShowHelixCtrlWheelHintBinding = new ValueBinding<bool>(ModId, "ShowHelixCtrlWheelHint", false));
            AddBinding(m_ShowSuperEllipseCtrlWheelHintBinding = new ValueBinding<bool>(ModId, "ShowSuperEllipseCtrlWheelHint", false));
            AddBinding(m_ActionStatusTextBinding = new ValueBinding<string>(ModId, "ActionStatusText", ""));


            // Global Triggers
            AddBinding(new TriggerBinding<string>(ModId, "ToggleTool", (toolName) => {
                MertToolState.UserJustChangedAssetCategory = false;
                CloseTools(ToolExitMode.UserSelectionClose);

                if (toolName == "Circle") m_CircleTool?.SetToolState(true);
                else if (toolName == "Helix") m_HelixTool?.SetToolState(true);
                else if (toolName == "SuperEllipse") m_SuperEllipseTool?.SetToolState(true);
                else if (toolName == "Grid") m_GridTool?.SetToolState(true);

                UpdateActiveToolState();
            }));

            AddBinding(new TriggerBinding(ModId, "UiInteracted", new Action(OnUiInteractedTriggered)));

            // Circle Triggers
            AddBinding(new TriggerBinding(ModId, "CircleDiameterUp", () => m_CircleTool?.QueueDiameterChange(+1)));
            AddBinding(new TriggerBinding(ModId, "CircleDiameterDown", () => m_CircleTool?.QueueDiameterChange(-1)));
            AddBinding(new TriggerBinding(ModId, "CircleDiameterStep", () => m_CircleTool?.QueueDiameterStepCycle()));
            AddBinding(new TriggerBinding<string>(ModId, "ToggleCircleSnap", (snapType) => m_CircleTool?.QueueSnapToggle(snapType)));
            AddBinding(new TriggerBinding(ModId, "ToggleCircleSubtract", () => m_CircleTool?.QueueSubtractToggle()));

            // Helix Triggers
            AddBinding(new TriggerBinding(ModId, "HelixDiameterUp", () => m_HelixTool?.QueueDiameterChange(+1)));
            AddBinding(new TriggerBinding(ModId, "HelixDiameterDown", () => m_HelixTool?.QueueDiameterChange(-1)));
            AddBinding(new TriggerBinding(ModId, "HelixDiameterStep", () => m_HelixTool?.QueueDiameterStepCycle()));
            AddBinding(new TriggerBinding(ModId, "HelixTurnsUp", () => m_HelixTool?.QueueTurnChange(+1)));
            AddBinding(new TriggerBinding(ModId, "HelixTurnsDown", () => m_HelixTool?.QueueTurnChange(-1)));
            AddBinding(new TriggerBinding(ModId, "HelixTurnsStep", () => m_HelixTool?.QueueTurnStepCycle()));
            AddBinding(new TriggerBinding(ModId, "HelixClearanceUp", () => m_HelixTool?.QueueClearanceChange(+1)));
            AddBinding(new TriggerBinding(ModId, "HelixClearanceDown", () => m_HelixTool?.QueueClearanceChange(-1)));
            AddBinding(new TriggerBinding(ModId, "HelixClearanceStep", () => m_HelixTool?.QueueClearanceStepCycle()));
            AddBinding(new TriggerBinding<string>(ModId, "HelixToggleSnap", (snapType) => m_HelixTool?.QueueSnapToggle(snapType)));

            // SuperEllipse Triggers
            AddBinding(new TriggerBinding(ModId, "SuperEllipseWidthUp", () => m_SuperEllipseTool?.QueueWidthChange(+1)));
            AddBinding(new TriggerBinding(ModId, "SuperEllipseWidthDown", () => m_SuperEllipseTool?.QueueWidthChange(-1)));
            AddBinding(new TriggerBinding(ModId, "SuperEllipseWidthStep", () => m_SuperEllipseTool?.QueueWidthStepCycle()));
            AddBinding(new TriggerBinding(ModId, "SuperEllipseLengthUp", () => m_SuperEllipseTool?.QueueLengthChange(+1)));
            AddBinding(new TriggerBinding(ModId, "SuperEllipseLengthDown", () => m_SuperEllipseTool?.QueueLengthChange(-1)));
            AddBinding(new TriggerBinding(ModId, "SuperEllipseLengthStep", () => m_SuperEllipseTool?.QueueLengthStepCycle()));
            AddBinding(new TriggerBinding<float>(ModId, "SuperEllipseSetN", (value) => m_SuperEllipseTool?.SetNFromUi(value)));
            AddBinding(new TriggerBinding<string>(ModId, "SuperEllipseToggleSnap", (snapType) => m_SuperEllipseTool?.QueueSnapToggle(snapType)));
            AddBinding(new TriggerBinding(ModId, "ToggleEllipseSubtract", () => m_SuperEllipseTool?.QueueSubtractToggle()));

            // Grid Triggers
            AddBinding(new TriggerBinding(ModId, "GridBlockWidthUp", () => m_GridTool?.QueueBlockWidthChange(+1)));
            AddBinding(new TriggerBinding(ModId, "GridBlockWidthDown", () => m_GridTool?.QueueBlockWidthChange(-1)));
            AddBinding(new TriggerBinding(ModId, "GridBlockLengthUp", () => m_GridTool?.QueueBlockLengthChange(+1)));
            AddBinding(new TriggerBinding(ModId, "GridBlockLengthDown", () => m_GridTool?.QueueBlockLengthChange(-1)));
            AddBinding(new TriggerBinding(ModId, "GridColumnsUp", () => m_GridTool?.QueueColsChange(+1)));
            AddBinding(new TriggerBinding(ModId, "GridColumnsDown", () => m_GridTool?.QueueColsChange(-1)));
            AddBinding(new TriggerBinding(ModId, "GridRowsUp", () => m_GridTool?.QueueRowsChange(+1)));
            AddBinding(new TriggerBinding(ModId, "GridRowsDown", () => m_GridTool?.QueueRowsChange(-1)));
            AddBinding(new TriggerBinding(ModId, "GridToggleAlternating", () => m_GridTool?.QueueToggleAlternating()));
            AddBinding(new TriggerBinding(ModId, "GridToggleOrientation", () => m_GridTool?.QueueToggleOrientation()));
            AddBinding(new TriggerBinding<string>(ModId, "GridToggleSnap", (snapType) => m_GridTool?.QueueSnapToggle(snapType)));
        }

        #endregion

        #region 7. BINDING UPDATES (STATE SYNC)

        /// <summary>
        /// Synchronizes the internal C# state of the Circle Tool with the frontend UI bindings.
        /// </summary>
        private void UpdateCircleBindings()
        {
            if (m_CircleTool == null) return;

            m_CircleDiameterBinding.Update(m_CircleTool.GetCurrentDiameter());
            m_CircleDiameterStepIndexBinding.Update(m_CircleTool.GetCurrentDiameterStepIndex());
            m_CircleDiameterStepSizeBinding.Update(m_CircleTool.GetDiameterStepSize());
        }

        /// <summary>
        /// Synchronizes the internal C# state of the Helix Tool with the frontend UI bindings.
        /// </summary>
        private void UpdateHelixBindings()
        {
            if (m_HelixTool == null) return;

            m_HelixDiameterBinding.Update(m_HelixTool.GetCurrentDiameter());
            m_HelixDiameterStepIndexBinding.Update(m_HelixTool.GetCurrentDiameterStepIndex());
            m_HelixTurnsBinding.Update(m_HelixTool.GetCurrentTurns());
            m_HelixTurnStepIndexBinding.Update(m_HelixTool.GetCurrentTurnStepIndex());
            m_HelixClearanceBinding.Update(m_HelixTool.GetCurrentClearance());
            m_HelixClearanceStepIndexBinding.Update(m_HelixTool.GetCurrentClearanceStepIndex());
        }

        /// <summary>
        /// Synchronizes the internal C# state of the SuperEllipse Tool with the frontend UI bindings.
        /// </summary>
        private void UpdateSuperEllipseBindings()
        {
            if (m_SuperEllipseTool == null) return;

            m_SuperEllipseWidthBinding.Update(m_SuperEllipseTool.GetCurrentWidth());
            m_SuperEllipseWidthStepIndexBinding.Update(m_SuperEllipseTool.GetCurrentWidthStepIndex());
            m_SuperEllipseLengthBinding.Update(m_SuperEllipseTool.GetCurrentLength());
            m_SuperEllipseLengthStepIndexBinding.Update(m_SuperEllipseTool.GetCurrentLengthStepIndex());
            m_SuperEllipseNBinding.Update(m_SuperEllipseTool.GetCurrentNSliderValue());
        }

        /// <summary>
        /// Synchronizes the internal C# state of the Grid Tool with the frontend UI bindings.
        /// </summary>
        private void UpdateGridBindings()
        {
            if (m_GridTool == null) return;

            m_GridBlockWidthBinding.Update(m_GridTool.GetCurrentBlockWidthU());
            m_GridBlockLengthBinding.Update(m_GridTool.GetCurrentBlockLengthU());
            m_GridColumnsBinding.Update(m_GridTool.GetCurrentColumns());
            m_GridRowsBinding.Update(m_GridTool.GetCurrentRows());
            m_GridAlternatingBinding.Update(m_GridTool.GetIsAlternating());
            m_GridOrientationLeftBottomBinding.Update(m_GridTool.GetIsOrientationLeftBottom());
            m_GridIsOneWaySupportedBinding.Update(m_GridTool.IsCurrentPrefabValidForOneWayPattern());
        }
        private void UpdateSharedSnapBindings()
        {
            MertBaseToolSystem activeTool = null;

            if (m_CircleTool != null && m_CircleTool.ToolEnabled)
                activeTool = m_CircleTool;
            else if (m_HelixTool != null && m_HelixTool.ToolEnabled)
                activeTool = m_HelixTool;
            else if (m_SuperEllipseTool != null && m_SuperEllipseTool.ToolEnabled)
                activeTool = m_SuperEllipseTool;
            else if (m_GridTool != null && m_GridTool.ToolEnabled)
                activeTool = m_GridTool;

            if (activeTool != null)
            {
                m_IsSnapGeometryActiveBinding.Update(activeTool.IsSnapGeometryEnabled());
                m_IsSnapNetSideActiveBinding.Update(activeTool.IsSnapNetSideEnabled());
                m_IsSnapNetAreaActiveBinding.Update(activeTool.IsSnapNetAreaEnabled());
            }
            else
            {
                m_IsSnapGeometryActiveBinding.Update(false);
                m_IsSnapNetSideActiveBinding.Update(false);
                m_IsSnapNetAreaActiveBinding.Update(false);
            }
        }
        /// <summary>
        /// Synchronizes the Ctrl+Mouse Wheel control state of the Grid Tool with the frontend UI bindings.
        /// </summary>
        private void UpdateActionHintBindings()
        {
            bool showCircleCtrlWheel = Mod.settings != null && Mod.settings.UseCtrlWheelForCircleDiameterAdjustment;
            bool showHelixCtrlWheel = Mod.settings != null && Mod.settings.UseCtrlWheelForHelixTurnAdjustment;
            bool showSuperEllipseCtrlWheel = Mod.settings != null && Mod.settings.UseCtrlWheelForShapeAdjustment;

            m_ShowCircleCtrlWheelHintBinding.Update(showCircleCtrlWheel);
            m_ShowHelixCtrlWheelHintBinding.Update(showHelixCtrlWheel);
            m_ShowSuperEllipseCtrlWheelHintBinding.Update(showSuperEllipseCtrlWheel);

            string statusText = string.Empty;

            string activeTool = "None";
            if (m_CircleTool != null && m_CircleTool.ToolEnabled) activeTool = "Circle";
            else if (m_HelixTool != null && m_HelixTool.ToolEnabled) activeTool = "Helix";
            else if (m_SuperEllipseTool != null && m_SuperEllipseTool.ToolEnabled) activeTool = "SuperEllipse";
            else if (m_GridTool != null && m_GridTool.ToolEnabled) activeTool = "Grid";

            switch (activeTool)
            {
                case "Circle":
                    if (m_CircleTool != null)
                    {
                        CircleMetrics m = m_CircleTool.GetCurrentCircleMetrics();
                        statusText =
                            $"Outer: {FormatSmart(m.OuterDiameterUnits)}U ({FormatSmart(m.OuterDiameterMeters)}m) - " +
                            $"Inner: {FormatSmart(m.InnerDiameterUnits)}U ({FormatSmart(m.InnerDiameterMeters)}m)";
                    }
                    break;

                case "Helix":
                    if (m_HelixTool != null)
                    {
                        statusText =
                            $"Diameter: {FormatSmart(m_HelixTool.GetCurrentDiameter())}m - " +
                            $"Turns: {FormatSmart(m_HelixTool.GetCurrentTurns())} - " +
                            $"Clearance: {FormatSmart(m_HelixTool.GetCurrentClearance())}m";
                    }
                    break;

                case "SuperEllipse":
                    if (m_SuperEllipseTool != null)
                    {
                        statusText =
                            $"Width: {FormatSmart(m_SuperEllipseTool.GetCurrentWidth())}m - " +
                            $"Length: {FormatSmart(m_SuperEllipseTool.GetCurrentLength())}m - " +
                            $"N: {FormatSmart(m_SuperEllipseTool.GetCurrentNSliderValue())}";
                    }
                    break;

                case "Grid":
                    if (m_GridTool != null)
                    {
                        statusText =
                            $"Width: {FormatSmart(m_GridTool.GetCurrentBlockWidthU())}U - " +
                            $"Length: {FormatSmart(m_GridTool.GetCurrentBlockLengthU())}U - " +
                            $"Rows: {FormatSmart(m_GridTool.GetCurrentRows())} - " +
                            $"Columns: {FormatSmart(m_GridTool.GetCurrentColumns())}";
                    }
                    break;
            }

            m_ActionStatusTextBinding.Update(statusText);
        }

        /// <summary>
        /// Determines which tool is currently active and updates the active tool binding string for the UI.
        /// </summary>
        private void UpdateActiveToolState()
        {
            if (MertToolState.SuppressUiAbortDuringRestore) return;

            string current = "None";
            if (m_CircleTool != null && m_CircleTool.ToolEnabled) current = "Circle";
            else if (m_HelixTool != null && m_HelixTool.ToolEnabled) current = "Helix";
            else if (m_SuperEllipseTool != null && m_SuperEllipseTool.ToolEnabled) current = "SuperEllipse";
            else if (m_GridTool != null && m_GridTool.ToolEnabled) current = "Grid";

            m_ActiveToolBinding.Update(current);
        }

        /// <summary>
        /// Evaluates if the toolbox UI should be allowed to render based on the validity of the currently selected prefab.
        /// </summary>
        private void UpdateToolBoxAllowedState()
        {
            bool isAllowed = m_CircleTool != null && m_CircleTool.IsCurrentPrefabValid();
            m_IsToolBoxAllowedBinding.Update(isAllowed);
        }

        #endregion

        #region 8. EVENT HANDLERS & TOOL MANAGEMENT

        /// <summary>
        /// Logs the exact timestamp of the last direct interaction with the UI, used for focus management.
        /// </summary>
        private void OnUiInteractedTriggered()
        {
            m_LastUiInteractionTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Handles forceful abort requests originating from the UI system.
        /// </summary>
        private void HandleToolAbortedByUi(ToolExitMode exitMode)
        {
            if (MertToolState.SuppressUiAbortDuringRestore) return;
            CloseTools(exitMode);
        }

        /// <summary>
        /// Disables all custom tools securely based on the provided exit mode and resets the active tool binding.
        /// </summary>
        private void CloseTools(ToolExitMode exitMode)
        {
            m_CircleTool?.RequestDisable(exitMode);
            m_HelixTool?.RequestDisable(exitMode);
            m_SuperEllipseTool?.RequestDisable(exitMode);
            m_GridTool?.RequestDisable(exitMode);

            m_ActiveToolBinding.Update("None");
            UpdateActiveToolState();
        }

        /// <summary>
        /// Listens to global tool changes and safely closes custom tools if standard game tools (like the Default Tool or an unsupported type) take over.
        /// Handles escape key closures and asset category switching.
        /// </summary>
        private void OnToolChanged(ToolBaseSystem tool)
        {
            if (MertToolState.SuppressUiAbortDuringRestore) return;

            var objectTool = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            var netTool = World.GetOrCreateSystemManaged<NetToolSystem>();
            var defaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();

            if (tool == defaultTool)
            {
                if (MertToolState.UserJustChangedAssetCategory) return;

                CloseTools(ToolExitMode.RestoreFromEscape);
                return;
            }

            if (MertToolState.UserJustChangedAssetCategory)
            {
                if (tool is NetToolSystem) MertToolState.UserJustChangedAssetCategory = false;
                return;
            }

            if (tool != objectTool && tool != netTool)
            {
                CloseTools(ToolExitMode.UserSelectionClose);
            }
        }

        #endregion

        #region 9. HELPERS
        private void UpdateSharedSubtractBinding()
        {
            bool isSubtractActive = false;

            if (m_CircleTool != null && m_CircleTool.ToolEnabled)
                isSubtractActive = m_CircleTool.IsSubtractEnabled();
            else if (m_SuperEllipseTool != null && m_SuperEllipseTool.ToolEnabled)
                isSubtractActive = m_SuperEllipseTool.IsSubtractEnabled();

            m_IsSubtractActiveBinding.Update(isSubtractActive);
        }
        private static string FormatSmart(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatSmart(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        #endregion
    }
}