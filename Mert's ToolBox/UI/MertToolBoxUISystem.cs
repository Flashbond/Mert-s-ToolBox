using Colossal.UI.Binding;
using Game.Input;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using MertsToolBox.Core;
using MertsToolBox.Management;
using MertsToolBox.Settings;
using MertsToolBox.Systems;
using MertsToolBox.UI;
using MertsToolBox.Utilities;
using MertsToolBox.Utilities.Preset;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.Entities;
using UnityEngine.Scripting;

namespace MertsToolBox
{
    public partial class MertToolBoxUISystem : UISystemBase
    {
        #region Constants & Fields
        private const string ModId = "MertsToolBox";
        private ToolSystem m_ToolSystem;

        private ShapeToolSystem m_Shape;
        private HelixToolSystem m_Helix;
        private SoftBlockToolSystem m_SoftBlock;
        private GridToolSystem m_Grid;

        private MertMutableBinding<string> m_ToolListBinding;
        private NetPrefab m_LastToolListPrefab;
        private bool m_LastIsToolBoxAllowed;

        private static readonly float[] s_DefaultElevationSteps = new float[] { 10f, 5f, 2.5f, 1.25f };
        private string m_LastActiveToolPipe = "None|None";
        private MertMutableBinding<string> m_PresetListBinding;
        private string m_LastToolListPipe = "";

        private ProxyAction m_OpenShapeToolAction;
        private ProxyAction m_OpenHelixToolAction;
        private ProxyAction m_OpenSoftBlockToolAction;
        private ProxyAction m_OpenGridToolAction;
        private ProxyAction m_UndoToolParameterAction;
        private ProxyAction m_RedoToolParameterAction;
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
        public class MertMutableBinding<T> : ValueBinding<T>
        {
            public MertMutableBinding(string group, string name, T initialValue = default)
                : base(group, name, initialValue)
            {
            }

            public void SetValue(T value)
            {
                base.Update(value);
            }
        }
        private void RefreshToolListBinding()
        {
            if (m_ToolListBinding == null)
                return;

            m_ToolListBinding.Update(GetToolListPipe());
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

            m_Shape = World.GetOrCreateSystemManaged<ShapeToolSystem>();
            m_Helix = World.GetOrCreateSystemManaged<HelixToolSystem>();
            m_SoftBlock = World.GetOrCreateSystemManaged<SoftBlockToolSystem>();
            m_Grid = World.GetOrCreateSystemManaged<GridToolSystem>();

            if (m_ToolSystem != null)
                m_ToolSystem.EventToolChanged += OnToolChanged;

            RegisterBindings();

            m_OpenShapeToolAction = Mod.settings?.GetAction(ToolBoxSettings.OpenShapeTool);
            m_OpenHelixToolAction = Mod.settings?.GetAction(ToolBoxSettings.OpenHelixTool);
            m_OpenSoftBlockToolAction = Mod.settings?.GetAction(ToolBoxSettings.OpenSoftBlockTool);
            m_OpenGridToolAction = Mod.settings?.GetAction(ToolBoxSettings.OpenGridTool);
            m_UndoToolParameterAction = Mod.settings?.GetAction(ToolBoxSettings.UndoToolParameter);
            m_RedoToolParameterAction = Mod.settings?.GetAction(ToolBoxSettings.RedoToolParameter);
            EnableAction(m_OpenShapeToolAction);
            EnableAction(m_OpenHelixToolAction);
            EnableAction(m_OpenSoftBlockToolAction);
            EnableAction(m_OpenGridToolAction);
            EnableAction(m_UndoToolParameterAction);
            EnableAction(m_RedoToolParameterAction);

            RefreshToolListBinding();

        }

        /// <summary>
        /// Continuously updates the global UI state and synchronizes individual tool bindings every frame.
        /// </summary>
        protected override void OnUpdate()
        {
            base.OnUpdate();

            MertToolState.HasReleasedStaleObjectToolThisFrame = false;

            if (!MertToolState.ControlledClearSelectionReplay &&
                !MertToolState.ControlledSelectAssetReplay &&
                !MertToolState.ControlledSelectCategoryReplay)
            {
                TryReleaseStaleStampAfterReload();
            }

            ProcessHotkeys();
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

            base.OnDestroy();
        }
        #endregion

        #region UI Bindings Registration
        private void ProcessHotkeys()
        {
            MertBaseToolSystem activeTool = GetActiveCustomTool();

            if (activeTool != null)
            {
                if (m_UndoToolParameterAction != null && m_UndoToolParameterAction.WasPressedThisFrame())
                    activeTool.UndoToolParameter();

                if (m_RedoToolParameterAction != null && m_RedoToolParameterAction.WasPressedThisFrame())
                    activeTool.RedoToolParameter();

                return;
            }

            if (m_OpenShapeToolAction != null && m_OpenShapeToolAction.WasPressedThisFrame())
                OpenToolFromHotkey(m_Shape?.ToolId);

            if (m_OpenHelixToolAction != null && m_OpenHelixToolAction.WasPressedThisFrame())
                OpenToolFromHotkey(m_Helix?.ToolId);

            if (m_OpenSoftBlockToolAction != null && m_OpenSoftBlockToolAction.WasPressedThisFrame())
                OpenToolFromHotkey(m_SoftBlock?.ToolId);

            if (m_OpenGridToolAction != null && m_OpenGridToolAction.WasPressedThisFrame())
                OpenToolFromHotkey(m_Grid?.ToolId);
        }
        private static void EnableAction(ProxyAction action)
        {
            if (action != null)
                action.shouldBeEnabled = true;
        }
        /// <summary>
        /// Registers all initial ValueBindings and TriggerBindings connecting the C# backend to the Cohtml/TSX frontend.
        /// </summary>
        private void RegisterBindings()
        {
            m_ToolListBinding = new MertMutableBinding<string>(
                ModId,
                "ToolList",
                GetToolListPipe()
            );

            AddBinding(m_ToolListBinding);

            AddUpdateBinding(new MertPolledBinding<string>(ModId, "ActiveTool", GetActiveToolPipe, "None|None"));

            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "IsToolBoxAllowed", GetIsToolBoxAllowed, false));

            // Shape Bindings
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "ShapeDimension",
                () => m_Shape?.GetCurrentDimension() ?? 96));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "ShapeDimensionStepValue",
                () => m_Shape?.GetDimensionStepSize() ?? 8));
            AddBinding(new ValueBinding<int[]>(
                ModId,
                "ShapeDimensionStepArray",
                m_Shape?.m_DimensionSteps ?? Array.Empty<int>(),
                new ArrayWriter<int>()
            ));
            AddBinding(new ValueBinding<string[]>(
                ModId,
                "ShapeNamesArray",
                ShapeToolSystem.ShapeDefinitions.Select(s => s.Name).ToArray(),
                new ArrayWriter<string>()
            ));
            AddUpdateBinding(new MertPolledBinding<int>(
                ModId,
                "ShapeCurrentIndex",
                () => m_Shape?.GetCurrentSidesIndex() ?? 0
            ));
            AddUpdateBinding(new MertPolledBinding<int>(
                ModId,
                "ShapeMaxIndex",
                () => ShapeToolSystem.ShapeDefinitions.Length - 1
            ));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "ShapeIsOneWaySupported", () => m_Shape?.IsCurrentPrefabValidForOneWayPattern() ?? false));

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
                () => m_Helix?.GetCurrentClearance() ?? 9f));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "HelixClearanceStepValue",
                () => m_Helix?.GetClearanceStepSize() ?? 2));
            AddBinding(new ValueBinding<float[]>(
                ModId,
                "HelixClearanceStepArray",
                m_Helix?.m_ClearanceSteps,
                new ArrayWriter<float>()
            ));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "HelixIsOneWaySupported",
                () => m_Helix?.IsCurrentPrefabValidForOneWayPattern() ?? false));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "HelixIsClockwise",
                () => m_Helix?.GetIsClockwise() ?? true));
            // Helix Build Option Bindings
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "HelixAnarchyInstalled",
                () => MertHelixBuildOption.IsAnarchyInstalled, false));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "HelixBuildMode",
                () => (int)MertHelixBuildOption.Current, 0));

            // SoftBlock Bindings
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "SoftBlockWidth",
                () => m_SoftBlock?.GetCurrentWidth() ?? 96));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "SoftBlockWidthStepValue",
                () => m_SoftBlock?.GetWidthStepSize() ?? 8));
            AddBinding(new ValueBinding<int[]>(
                ModId,
                "SoftBlockWidthStepArray",
                m_SoftBlock?.m_WidthSteps,
                new ArrayWriter<int>()
            ));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "SoftBlockLength",
                () => m_SoftBlock?.GetCurrentLength() ?? 192));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "SoftBlockLengthStepValue",
                () => m_SoftBlock?.GetLengthStepSize() ?? 8));
            AddBinding(new ValueBinding<int[]>(
                ModId,
                "SoftBlockLengthStepArray",
                m_SoftBlock?.m_LengthSteps,
                new ArrayWriter<int>()
            ));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "GetBorderRadius",
                         () => m_SoftBlock?.GetCurrentBorderRadius() ?? 5f));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "SoftBlockStraightCorners",
                () => m_SoftBlock != null && m_SoftBlock.GetUseStraightCorners()));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "SoftBlockStraightCornersSupported",
                () => m_SoftBlock != null && m_SoftBlock.IsStraightCornersSupported()));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "SoftBlockIsOneWaySupported", () => m_SoftBlock?.IsCurrentPrefabValidForOneWayPattern() ?? false));

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

            // Direction Bindings
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "MainDirectionState",
                () => GetEnabledTool()?.GetMainDirectionState() ?? true));


            // Crosswalk Bindings
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "SuppressCrosswalks",
            () => MertToolState.SuppressCrosswalks, false));

            // Elevation Bindings
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "ElevationStepValue",
                () => GetEnabledTool()?.GetElevationStepValue() ?? 10f));
            AddBinding(new ValueBinding<float[]>(
                ModId,
                "ElevationStepArray",
                GetCurrentElevationSteps(),
                new ArrayWriter<float>()
            ));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "ElevationValue",
                () => GetEnabledTool()?.GetCurrentNetToolElevation() ?? 0f));

            // Shared Snap & Toggle Bindings
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "IsSnapGeometryActive",
                () => GetEnabledTool()?.IsSnapGeometryEnabled() ?? false));

            // Flatten Toggle Binding
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "IsFlattenActive",
                () => MertToolState.FlattenGeometryEnabled, false));

            // TrafficLights Toggle Binding
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "SuppressTrafficLights",
                () => MertToolState.SuppressTrafficLights, false));

            // Action Hints
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "ShowShapeCtrlWheelHint",
                () => Mod.settings != null && Mod.settings.UseCtrlWheelForShapeDimensionAdjustment));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "ShowHelixCtrlWheelHint",
                () => Mod.settings != null && Mod.settings.UseCtrlWheelForHelixTurnAdjustment));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "ShowSoftBlockCtrlWheelHint",
                () => Mod.settings != null && Mod.settings.UseCtrlWheelForSoftBlockBorderRadius));

            AddUpdateBinding(new MertPolledBinding<string>(ModId, "ActionStatusText",
                () => GetActionStatusText(), ""));

            // Preset Bindings
            m_PresetListBinding = new MertMutableBinding<string>(ModId, "PresetList", "");
            AddBinding(m_PresetListBinding);

            // Global Triggers
            AddBinding(new TriggerBinding<string>(ModId, "ToggleTool", (toolId) =>
            {
                MertToolState.ClearControlledReplayFlags();
                CloseTools(ToolExitMode.UserSelectionClose);

                if (toolId == m_Shape?.ToolId)
                    m_Shape.SetToolState(true);
                else if (toolId == m_Helix?.ToolId)
                    m_Helix.SetToolState(true);
                else if (toolId == m_SoftBlock?.ToolId)
                    m_SoftBlock.SetToolState(true);
                else if (toolId == m_Grid?.ToolId)
                    m_Grid.SetToolState(true);
            }));

            AddBinding(new TriggerBinding<float>(ModId, "ElevationStep",
                val => GetEnabledTool()?.SetElevationStepFromUi(val)));

            AddBinding(new TriggerBinding(ModId, "ElevationUp",
                () => GetEnabledTool()?.QueueElevationChangeFromUi(+1)));

            AddBinding(new TriggerBinding(ModId, "ElevationDown",
                () => GetEnabledTool()?.QueueElevationChangeFromUi(-1)));

            // Snap Trigger
            AddBinding(new TriggerBinding(ModId, "ToggleSnap",
                () => GetEnabledTool()?.QueueSnapToggle()));

            // Direction Trigger
            AddBinding(new TriggerBinding(ModId, "ToggleMainDirection",
                () => GetEnabledTool()?.QueueToggleMainDirection()));

            // Crosswalk Trigger
            AddBinding(new TriggerBinding(ModId, "ToggleSuppressCrosswalks", () =>
            {
                MertToolState.SuppressCrosswalks = !MertToolState.SuppressCrosswalks;
                GetEnabledTool()?.QueuePreviewRebuild();
            }));

            // Flatten Trigger
            AddBinding(new TriggerBinding(ModId, "ToggleFlattenGeometry",
                () => GetEnabledTool()?.QueueFlattenToggle()));

            // TrafficLights Trigger
            AddBinding(new TriggerBinding(ModId, "ToggleSuppressTrafficLights", () =>
            {
                MertToolState.SuppressTrafficLights = !MertToolState.SuppressTrafficLights;
                GetEnabledTool()?.QueuePreviewRebuild();
            }));

            // Shape Triggers
            AddBinding(new TriggerBinding(ModId, "ShapeDimensionUp", () => m_Shape?.QueueDimensionChange(+1)));
            AddBinding(new TriggerBinding(ModId, "ShapeDimensionDown", () => m_Shape?.QueueDimensionChange(-1)));
            AddBinding(new TriggerBinding<int>(ModId, "ShapeDimensionStep", (val) => m_Shape?.QueueSetDimensionStep(val)));
            AddBinding(new TriggerBinding(ModId, "ShapeSidesUp", () => m_Shape?.QueueSidesChange(+1)));
            AddBinding(new TriggerBinding(ModId, "ShapeSidesDown", () => m_Shape?.QueueSidesChange(-1)));

            // Helix Triggers
            AddBinding(new TriggerBinding<int>(ModId, "HelixSetBuildMode", (mode) => MertHelixBuildOption.Select((HelixBuildMode)mode)));
            AddBinding(new TriggerBinding(ModId, "HelixDiameterUp", () => m_Helix?.QueueDiameterChange(+1)));
            AddBinding(new TriggerBinding(ModId, "HelixDiameterDown", () => m_Helix?.QueueDiameterChange(-1)));
            AddBinding(new TriggerBinding<int>(ModId, "HelixDiameterStep", (val) => m_Helix?.QueueSetDiameterStep(val)));
            AddBinding(new TriggerBinding(ModId, "HelixTurnsUp", () => m_Helix?.QueueTurnChange(+1)));
            AddBinding(new TriggerBinding(ModId, "HelixTurnsDown", () => m_Helix?.QueueTurnChange(-1)));
            AddBinding(new TriggerBinding<float>(ModId, "HelixTurnStep", (val) => m_Helix?.QueueSetTurnStep(val)));
            AddBinding(new TriggerBinding(ModId, "HelixClearanceUp", () => m_Helix?.QueueClearanceChange(+1)));
            AddBinding(new TriggerBinding(ModId, "HelixClearanceDown", () => m_Helix?.QueueClearanceChange(-1)));
            AddBinding(new TriggerBinding<float>(ModId, "HelixClearanceStep", (val) => m_Helix?.QueueSetClearanceStep(val)));
            AddBinding(new TriggerBinding(ModId, "HelixToggleDirection", () => m_Helix?.QueueToggleDirection()));

            // SoftBlock Triggers
            AddBinding(new TriggerBinding(ModId, "SoftBlockWidthUp", () => m_SoftBlock?.QueueWidthChange(+1)));
            AddBinding(new TriggerBinding(ModId, "SoftBlockWidthDown", () => m_SoftBlock?.QueueWidthChange(-1)));
            AddBinding(new TriggerBinding<int>(ModId, "SoftBlockWidthStep", (val) => m_SoftBlock?.QueueSetWidthStep(val)));
            AddBinding(new TriggerBinding(ModId, "SoftBlockLengthUp", () => m_SoftBlock?.QueueLengthChange(+1)));
            AddBinding(new TriggerBinding(ModId, "SoftBlockLengthDown", () => m_SoftBlock?.QueueLengthChange(-1)));
            AddBinding(new TriggerBinding<int>(ModId, "SoftBlockLengthStep", (val) => m_SoftBlock?.QueueSetLengthStep(val)));
            AddBinding(new TriggerBinding<float>(ModId, "SetBorderRadius", (value) => m_SoftBlock?.SetBorderRadiusFromUi(value)));
            AddBinding(new TriggerBinding(ModId, "BeginBorderRadiusDrag", () => m_SoftBlock?.BeginBorderRadiusDrag()));
            AddBinding(new TriggerBinding(ModId, "EndBorderRadiusDrag", () => m_SoftBlock?.EndBorderRadiusDrag()));
            AddBinding(new TriggerBinding(ModId, "SoftBlockToggleStraightCorners", () => m_SoftBlock?.QueueToggleStraightCorners()));

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

            // Preset Triggers
            AddBinding(new CallBinding<string, bool>(ModId, "SavePreset", (toolId) =>
            {
                try
                {
                    MertBaseToolSystem tool = GetToolById(toolId);
                    MertToolPreset preset = tool?.CreatePresetSnapshot();

                    if (preset != null)
                    {
                        return MertToolPresetStorage.SavePreset(preset);
                    }

                    return false;
                }
                catch (Exception e)
                {
                    ModRuntime.Warn($"[CallBinding] Save preset binding error: {e.Message}");
                    return false;
                }
            }));

            AddBinding(new TriggerBinding<string>(ModId, "LoadPreset", (presetDisplayName) =>
            {
                MertBaseToolSystem tool = GetActiveCustomTool();
                if (tool == null)
                    return;

                NetPrefab prefab = tool.GetCurrentSelectedNetPrefabForUi();
                if (prefab == null)
                    return;

                List<MertToolPreset> presets =
                    MertToolPresetStorage.LoadPresets(tool.ToolId, prefab.name);

                MertToolPreset preset = presets.FirstOrDefault(p => p.DisplayName == presetDisplayName);
                if (preset == null)
                    return;

                tool.ApplyPresetSnapshot(preset);
            }));
            AddBinding(new TriggerBinding(ModId, "RefreshPresetList", () => RefreshPresetListBinding()));
            AddBinding(new TriggerBinding<string>(ModId, "DeletePreset", (presetDisplayName) =>
            {
                MertBaseToolSystem tool = GetActiveCustomTool();
                if (tool == null) return;

                NetPrefab prefab = tool.GetCurrentSelectedNetPrefabForUi();
                if (prefab == null) return;

                bool deleted = MertToolPresetStorage.DeletePreset(
                    tool.ToolId,
                    prefab.name,
                    presetDisplayName
                );

                if (deleted)
                    RefreshPresetListBinding();
            }));
        }
        #endregion

        #region Tool Event Handling
        private float[] GetCurrentElevationSteps()
        {
            if (m_Shape != null && m_Shape.ToolEnabled) return m_Shape.GetElevationStepArray();
            if (m_Helix != null && m_Helix.ToolEnabled) return m_Helix.GetElevationStepArray();
            if (m_SoftBlock != null && m_SoftBlock.ToolEnabled) return m_SoftBlock.GetElevationStepArray();
            if (m_Grid != null && m_Grid.ToolEnabled) return m_Grid.GetElevationStepArray();

            return s_DefaultElevationSteps;
        }

        /// <summary>
        /// Disables all custom tools securely based on the provided exit mode.
        /// </summary>
        private void CloseTools(ToolExitMode exitMode)
        {
            m_Shape?.RequestDisable(exitMode);
            m_Helix?.RequestDisable(exitMode);
            m_SoftBlock?.RequestDisable(exitMode);
            m_Grid?.RequestDisable(exitMode);
        }

        /// <summary>
        /// Listens to global tool changes and safely closes custom tools if standard game tools take over.
        /// </summary>
        // OnToolChanged final minimal test hali

        private void OnToolChanged(ToolBaseSystem tool)
        {
            if (IsRoadBuilderTool(tool))
            {
                CloseTools(ToolExitMode.UserSelectionClose);
            }
        }

        private MertBaseToolSystem GetToolById(string toolId)
        {
            if (m_Shape != null && m_Shape.ToolId == toolId) return m_Shape;
            if (m_Helix != null && m_Helix.ToolId == toolId) return m_Helix;
            if (m_SoftBlock != null && m_SoftBlock.ToolId == toolId) return m_SoftBlock;
            if (m_Grid != null && m_Grid.ToolId == toolId) return m_Grid;
            return null;
        }
        private MertBaseToolSystem GetActiveCustomTool()
        {
            if (m_Shape != null && m_Shape.ToolEnabled) return m_Shape;
            if (m_Helix != null && m_Helix.ToolEnabled) return m_Helix;
            if (m_SoftBlock != null && m_SoftBlock.ToolEnabled) return m_SoftBlock;
            if (m_Grid != null && m_Grid.ToolEnabled) return m_Grid;

            return null;
        }
        private void OpenToolFromHotkey(string toolId)
        {
            if (string.IsNullOrEmpty(toolId))
                return;

            MertBaseToolSystem target = GetToolById(toolId);
            if (target == null)
                return;

            if (target.ToolEnabled)
                return;

            if (!target.IsCurrentPrefabValid())
                return;

            target.SetToolState(true);
        }
        #endregion

        #region External Selection Normalization
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
                CloseTools(ToolExitMode.VanillaToolbarClear);
            }
        }

        #endregion

        #region Helpers & UI Formatting Utilities
        private string GetActiveToolPipe()
        {
            string next = "None|None";

            if (m_Shape != null && m_Shape.ToolEnabled)
                next = $"{m_Shape.ToolId}|{m_Shape.ToolName}";
            else if (m_Helix != null && m_Helix.ToolEnabled)
                next = $"{m_Helix.ToolId}|{m_Helix.ToolName}";
            else if (m_SoftBlock != null && m_SoftBlock.ToolEnabled)
                next = $"{m_SoftBlock.ToolId}|{m_SoftBlock.ToolName}";
            else if (m_Grid != null && m_Grid.ToolEnabled)
                next = $"{m_Grid.ToolId}|{m_Grid.ToolName}";

            if (next == m_LastActiveToolPipe)
                return m_LastActiveToolPipe;

            m_LastActiveToolPipe = next;
            return next;
        }
        private string GetToolListPipe()
        {
            NetPrefab current =
                m_Helix?.GetCurrentSelectedNetPrefabForUi() ??
                m_SoftBlock?.GetCurrentSelectedNetPrefabForUi();

            bool isTrackLike =
                current != null &&
                MertBaseToolSystem.IsTrackLikePrefab(current);

            bool isPierLike =
                current != null &&
                MertBaseToolSystem.IsPierLikePrefab(current);

            string next;

            if (isTrackLike)
            {
                next = m_SoftBlock != null
                    ? $"{m_SoftBlock.ToolId}|{m_SoftBlock.ToolName}|{m_SoftBlock.ToolId}"
                    : "";
            }
            else if (isPierLike)
            {
                next = m_Helix != null
                    ? $"{m_Helix.ToolId}|{m_Helix.ToolName}|{m_Helix.ToolId}"
                    : "";
            }
            else
            {
                List<string> parts = new();

                if (m_Shape != null)
                    parts.Add($"{m_Shape.ToolId}|{m_Shape.ToolName}|{m_Shape.ToolId}");
                if (m_Helix != null)
                    parts.Add($"{m_Helix.ToolId}|{m_Helix.ToolName}|{m_Helix.ToolId}");
                if (m_SoftBlock != null)
                    parts.Add($"{m_SoftBlock.ToolId}|{m_SoftBlock.ToolName}|{m_SoftBlock.ToolId}");
                if (m_Grid != null)
                    parts.Add($"{m_Grid.ToolId}|{m_Grid.ToolName}|{m_Grid.ToolId}");

                next = string.Join(";", parts);
            }

            m_LastToolListPipe = next;
            return next;
        }
        private MertBaseToolSystem GetEnabledTool()
        {
            if (m_Shape?.ToolEnabled == true) return m_Shape;
            if (m_Helix?.ToolEnabled == true) return m_Helix;
            if (m_SoftBlock?.ToolEnabled == true) return m_SoftBlock;
            if (m_Grid?.ToolEnabled == true) return m_Grid;

            return null;
        }
        private bool GetIsToolBoxAllowed()
        {
            NetPrefab currentPrefab =
                m_Helix?.GetCurrentSelectedNetPrefabForUi() ??
                m_SoftBlock?.GetCurrentSelectedNetPrefabForUi();

            if (currentPrefab != m_LastToolListPrefab)
            {
                m_LastToolListPrefab = currentPrefab;
                RefreshToolListBinding();
            }

            bool prefabValid =
                (m_Shape != null && m_Shape.IsCurrentPrefabValid()) ||
                (m_Helix != null && m_Helix.IsCurrentPrefabValid()) ||
                (m_SoftBlock != null && m_SoftBlock.IsCurrentPrefabValid()) ||
                (m_Grid != null && m_Grid.IsCurrentPrefabValid());

            if (!prefabValid)
            {
                m_LastIsToolBoxAllowed = false;
                return false;
            }

            bool anyToolEnabled =
                (m_Shape != null && m_Shape.ToolEnabled) ||
                (m_Helix != null && m_Helix.ToolEnabled) ||
                (m_SoftBlock != null && m_SoftBlock.ToolEnabled) ||
                (m_Grid != null && m_Grid.ToolEnabled);

            bool toolContextValid =
                m_ToolSystem != null &&
                (m_ToolSystem.activeTool is NetToolSystem || anyToolEnabled);

            m_LastIsToolBoxAllowed = prefabValid && toolContextValid;
            return m_LastIsToolBoxAllowed;
        }

        /// <summary>
        /// Generates the formatted status text displaying current metrics for the active tool.
        /// </summary>
        private string GetActionStatusText()
        {
            if (m_Shape != null && m_Shape.ToolEnabled)
            {
                ShapeMetrics m = m_Shape.GetCurrentShapeMetrics();
                return $"Outer: {FormatSmart(m.OuterDimensionUnits)}U ({FormatSmart(m.OuterDimensionMeters)}m) - " +
                       $"Inner: {FormatSmart(m.InnerDimensionUnits)}U ({FormatSmart(m.InnerDiameterMeters)}m)";
            }
            if (m_Helix != null && m_Helix.ToolEnabled)
            {
                return $"Diameter: {FormatSmart(m_Helix.GetCurrentDiameter())}m - " +
                       $"Turns: {FormatSmart(m_Helix.GetCurrentTurns())} - " +
                       $"Clearance: {FormatSmart(m_Helix.GetCurrentClearance())}m";
            }
            if (m_SoftBlock != null && m_SoftBlock.ToolEnabled)
            {
                return $"Width: {FormatSmart(m_SoftBlock.GetCurrentWidth())}m - " +
                       $"Length: {FormatSmart(m_SoftBlock.GetCurrentLength())}m - " +
                       $"Radius: {FormatSmart(m_SoftBlock.GetCurrentBorderRadius())}";
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

        #region Preset System
        private void RefreshPresetListBinding()
        {
            MertBaseToolSystem tool = GetActiveCustomTool();
            if (tool == null)
            {
                m_PresetListBinding?.SetValue("");
                return;
            }

            NetPrefab prefab = tool.GetCurrentSelectedNetPrefabForUi();
            if (prefab == null)
            {
                m_PresetListBinding?.SetValue("");
                return;
            }

            List<MertToolPreset> presets = MertToolPresetStorage.LoadPresets(tool.ToolId, prefab.name);

            string value = string.Join(";", presets.Select(p =>
                $"{ToPresetDisplayLabel(p.DisplayName)}|{p.DisplayName}"));

            m_PresetListBinding?.SetValue(value);
        }

        private static string ToPresetDisplayLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            return value.Replace("_", " ");
        }
        #endregion

    }
}