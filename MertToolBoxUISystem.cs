using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using System;
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

        private CircleToolSystem m_CircleTool;
        private HelixToolSystem m_HelixTool;
        private SuperEllipseToolSystem m_SuperEllipseTool;
        private GridToolSystem m_GridTool;
        #endregion

        #region Nested Types
        /// <summary>
        /// A generic polling binding class used to synchronize backend values with the UI automatically.
        /// </summary>
        public class MertPolledBinding<T> : ValueBinding<T>, IUpdateBinding
        {
            private Func<T> m_Getter;

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

            MertToolState.HasReleasedStaleObjectToolThisFrame = false;
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

            m_ToolSystem = null;
            m_CircleTool = null;
            m_HelixTool = null;
            m_SuperEllipseTool = null;
            m_GridTool = null;

            base.OnDestroy();
        }
        #endregion

        #region UI Bindings Registration
        /// <summary>
        /// Registers all initial ValueBindings and TriggerBindings connecting the C# backend to the Cohtml/TSX frontend.
        /// </summary>
        private void RegisterBindings()
        {
            AddUpdateBinding(new MertPolledBinding<string>(ModId, "ActiveTool", () =>
            {
                if (m_CircleTool != null && m_CircleTool.ToolEnabled) return "Circle";
                if (m_HelixTool != null && m_HelixTool.ToolEnabled) return "Helix";
                if (m_SuperEllipseTool != null && m_SuperEllipseTool.ToolEnabled) return "SuperEllipse";
                if (m_GridTool != null && m_GridTool.ToolEnabled) return "Grid";
                return "None";
            }, "None"));

            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "IsToolBoxAllowed", () =>
            {
                bool prefabValid = m_CircleTool != null && m_CircleTool.IsCurrentPrefabValid();
                bool toolContextValid = m_ToolSystem != null && (
                    m_ToolSystem.activeTool is NetToolSystem ||
                    (m_CircleTool != null && m_CircleTool.ToolEnabled) ||
                    (m_HelixTool != null && m_HelixTool.ToolEnabled) ||
                    (m_SuperEllipseTool != null && m_SuperEllipseTool.ToolEnabled) ||
                    (m_GridTool != null && m_GridTool.ToolEnabled)
                );
                return prefabValid && toolContextValid;
            }, false));

            // Circle Bindings
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "CircleDiameter",
                () => m_CircleTool != null ? m_CircleTool.GetCurrentDiameter() : 80f));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "CircleDiameterStepIndex",
                () => m_CircleTool != null ? m_CircleTool.GetCurrentDiameterStepIndex() : 3));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "CircleDiameterStepSize",
                () => m_CircleTool != null ? m_CircleTool.GetDiameterStepSize() : 8));

            // Helix Bindings
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "HelixDiameter",
                () => m_HelixTool != null ? m_HelixTool.GetCurrentDiameter() : 80f));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "HelixDiameterStepIndex",
                () => m_HelixTool != null ? m_HelixTool.GetCurrentDiameterStepIndex() : 0));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "HelixTurns",
                () => m_HelixTool != null ? m_HelixTool.GetCurrentTurns() : 3f));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "HelixTurnStepIndex",
                () => m_HelixTool != null ? m_HelixTool.GetCurrentTurnStepIndex() : 0));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "HelixClearance",
                () => m_HelixTool != null ? m_HelixTool.GetCurrentClearance() : 8f));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "HelixClearanceStepIndex",
                () => m_HelixTool != null ? m_HelixTool.GetCurrentClearanceStepIndex() : 0));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "HelixDiameterStepSize",
                () => m_HelixTool != null ? m_HelixTool.GetDiameterStepSize() : 8));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "HelixTurnStepSize",
                () => m_HelixTool != null ? m_HelixTool.GetTurnStepSize() : 2f));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "HelixClearanceStepSize",
                () => m_HelixTool != null ? m_HelixTool.GetClearanceStepSize() : 2f));

            // SuperEllipse Bindings
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "SuperEllipseWidth",
                () => m_SuperEllipseTool != null ? m_SuperEllipseTool.GetCurrentWidth() : 80f));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "SuperEllipseWidthStepIndex",
                () => m_SuperEllipseTool != null ? m_SuperEllipseTool.GetCurrentWidthStepIndex() : 0));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "SuperEllipseLength",
                () => m_SuperEllipseTool != null ? m_SuperEllipseTool.GetCurrentLength() : 160f));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "SuperEllipseLengthStepIndex",
                () => m_SuperEllipseTool != null ? m_SuperEllipseTool.GetCurrentLengthStepIndex() : 0));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "SuperEllipseN",
                () => m_SuperEllipseTool != null ? m_SuperEllipseTool.GetCurrentNSliderValue() : 8f));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "SuperEllipseWidthStepSize",
                () => m_SuperEllipseTool != null ? m_SuperEllipseTool.GetWidthStepSize() : 8f));
            AddUpdateBinding(new MertPolledBinding<float>(ModId, "SuperEllipseLengthStepSize",
                () => m_SuperEllipseTool != null ? m_SuperEllipseTool.GetLengthStepSize() : 8f));

            // Grid Bindings
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "GridBlockWidth",
                () => m_GridTool != null ? m_GridTool.GetCurrentBlockWidthU() : 12));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "GridBlockLength",
                () => m_GridTool != null ? m_GridTool.GetCurrentBlockLengthU() : 12));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "GridColumns",
                () => m_GridTool != null ? m_GridTool.GetCurrentColumns() : 2));
            AddUpdateBinding(new MertPolledBinding<int>(ModId, "GridRows",
                () => m_GridTool != null ? m_GridTool.GetCurrentRows() : 2));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "GridAlternating",
                () => m_GridTool != null && m_GridTool.GetIsAlternating()));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "GridOrientationLeftBottom",
                () => m_GridTool != null && m_GridTool.GetIsOrientationLeftBottom()));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "GridIsOneWaySupported",
                () => m_GridTool != null && m_GridTool.IsCurrentPrefabValidForOneWayPattern()));

            // Shared Snap & Toggle Bindings
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "ShowSnapRow", () =>
            {
                if (m_GridTool != null && m_GridTool.ToolEnabled)
                    return Mod.settings != null && Mod.settings.EnableGridSnap;

                if (m_HelixTool != null && m_HelixTool.ToolEnabled)
                    return Mod.settings != null && Mod.settings.EnableHelixSnap;
                return true;
            }));

            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "IsSnapGeometryActive", () => {
                if (m_CircleTool != null && m_CircleTool.ToolEnabled) return m_CircleTool.IsSnapGeometryEnabled();
                if (m_HelixTool != null && m_HelixTool.ToolEnabled) return m_HelixTool.IsSnapGeometryEnabled();
                if (m_SuperEllipseTool != null && m_SuperEllipseTool.ToolEnabled) return m_SuperEllipseTool.IsSnapGeometryEnabled();
                if (m_GridTool != null && m_GridTool.ToolEnabled) return m_GridTool.IsSnapGeometryEnabled();
                return false;
            }));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "IsSnapNetSideActive", () => {
                if (m_CircleTool != null && m_CircleTool.ToolEnabled) return m_CircleTool.IsSnapNetSideEnabled();
                if (m_HelixTool != null && m_HelixTool.ToolEnabled) return m_HelixTool.IsSnapNetSideEnabled();
                if (m_SuperEllipseTool != null && m_SuperEllipseTool.ToolEnabled) return m_SuperEllipseTool.IsSnapNetSideEnabled();
                if (m_GridTool != null && m_GridTool.ToolEnabled) return m_GridTool.IsSnapNetSideEnabled();
                return false;
            }));
            AddUpdateBinding(new MertPolledBinding<bool>(ModId, "IsSnapNetAreaActive", () =>
            {
                if (m_CircleTool != null && m_CircleTool.ToolEnabled) return m_CircleTool.IsSnapNetAreaEnabled();
                if (m_HelixTool != null && m_HelixTool.ToolEnabled) return m_HelixTool.IsSnapNetAreaEnabled();
                if (m_SuperEllipseTool != null && m_SuperEllipseTool.ToolEnabled) return m_SuperEllipseTool.IsSnapNetAreaEnabled();
                if (m_GridTool != null && m_GridTool.ToolEnabled) return m_GridTool.IsSnapNetAreaEnabled();
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
            AddBinding(new TriggerBinding<string>(ModId, "ToggleTool", (toolName) =>
            {
                MertToolState.UserJustChangedAssetCategory = false;
                CloseTools(ToolExitMode.UserSelectionClose);

                if (toolName == "Circle") m_CircleTool?.SetToolState(true);
                else if (toolName == "Helix") m_HelixTool?.SetToolState(true);
                else if (toolName == "SuperEllipse") m_SuperEllipseTool?.SetToolState(true);
                else if (toolName == "Grid") m_GridTool?.SetToolState(true);
            }));

            // Circle Triggers
            AddBinding(new TriggerBinding(ModId, "CircleDiameterUp", () => m_CircleTool?.QueueDiameterChange(+1)));
            AddBinding(new TriggerBinding(ModId, "CircleDiameterDown", () => m_CircleTool?.QueueDiameterChange(-1)));
            AddBinding(new TriggerBinding(ModId, "CircleDiameterStep", () => m_CircleTool?.QueueDiameterStepCycle()));
            AddBinding(new TriggerBinding<string>(ModId, "ToggleCircleSnap", (snapType) => m_CircleTool?.QueueSnapToggle(snapType)));

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
            m_CircleTool?.RequestDisable(exitMode);
            m_HelixTool?.RequestDisable(exitMode);
            m_SuperEllipseTool?.RequestDisable(exitMode);
            m_GridTool?.RequestDisable(exitMode);
        }

        /// <summary>
        /// Listens to global tool changes and safely closes custom tools if standard game tools take over.
        /// </summary>
        private void OnToolChanged(ToolBaseSystem tool)
        {
            string incoming =
                tool == null ? "NULL" :
                tool.GetType().Name;

            string active =
                m_ToolSystem?.activeTool == null ? "NULL" :
                m_ToolSystem.activeTool.GetType().Name;

            if (MertToolState.SuppressToolChangedDuringColdstart)
                return;
            if (MertToolState.SuppressUiAbortDuringRestore)
                return;

            var objectTool = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            var netTool = World.GetOrCreateSystemManaged<NetToolSystem>();
            var defaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();

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
                CloseTools(ToolExitMode.UserSelectionClose);
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
                            $"[MertsToolBox] Restore category failed: {tie.InnerException?.Message ?? tie.Message}");
                    }
                    catch (Exception e)
                    {
                        ModRuntime.Warn(
                            $"[MertsToolBox] Restore category failed: {e.Message}");
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
                                $"[MertsToolBox] Restore asset failed: {tie.InnerException?.Message ?? tie.Message}");
                        }
                        catch (Exception e)
                        {
                            ModRuntime.Warn(
                                $"[MertsToolBox] Restore asset failed: {e.Message}");
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
                        ModRuntime.Warn($"[MertsToolBox] ActivatePrefabTool failed during restore: {e.Message}");
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

        #region UI Formatting Utilities
        /// <summary>
        /// Generates the formatted status text displaying current metrics for the active tool.
        /// </summary>
        private string GetActionStatusText()
        {
            if (m_CircleTool != null && m_CircleTool.ToolEnabled)
            {
                CircleMetrics m = m_CircleTool.GetCurrentCircleMetrics();
                return $"Outer: {FormatSmart(m.OuterDiameterUnits)}U ({FormatSmart(m.OuterDiameterMeters)}m) - " +
                       $"Inner: {FormatSmart(m.InnerDiameterUnits)}U ({FormatSmart(m.InnerDiameterMeters)}m)";
            }
            if (m_HelixTool != null && m_HelixTool.ToolEnabled)
            {
                return $"Diameter: {FormatSmart(m_HelixTool.GetCurrentDiameter())}m - " +
                       $"Turns: {FormatSmart(m_HelixTool.GetCurrentTurns())} - " +
                       $"Clearance: {FormatSmart(m_HelixTool.GetCurrentClearance())}m";
            }
            if (m_SuperEllipseTool != null && m_SuperEllipseTool.ToolEnabled)
            {
                return $"Width: {FormatSmart(m_SuperEllipseTool.GetCurrentWidth())}m - " +
                       $"Length: {FormatSmart(m_SuperEllipseTool.GetCurrentLength())}m - " +
                       $"N: {FormatSmart(m_SuperEllipseTool.GetCurrentNSliderValue())}";
            }
            if (m_GridTool != null && m_GridTool.ToolEnabled)
            {
                return $"Width: {FormatSmart(m_GridTool.GetCurrentBlockWidthU())}U - " +
                       $"Length: {FormatSmart(m_GridTool.GetCurrentBlockLengthU())}U - " +
                       $"Rows: {FormatSmart(m_GridTool.GetCurrentRows())} - " +
                       $"Columns: {FormatSmart(m_GridTool.GetCurrentColumns())}";
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