using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using System;

namespace MertsToolBox.Settings
{
    [FileLocation("ModsSettings/MertsToolBox/MertsToolBox")]
    [SettingsUIKeyboardAction(OpenShapeTool, ActionType.Button, usages: new string[] { "Tool" })]
    [SettingsUIKeyboardAction(OpenHelixTool, ActionType.Button, usages: new string[] { "Tool" })]
    [SettingsUIKeyboardAction(OpenSoftBlockTool, ActionType.Button, usages: new string[] { "Tool" })]
    [SettingsUIKeyboardAction(OpenGridTool, ActionType.Button, usages: new string[] { "Tool" })]
    [SettingsUIKeyboardAction(UndoToolParameter, ActionType.Button, usages: new string[] { "Tool" })]
    [SettingsUIKeyboardAction(RedoToolParameter, ActionType.Button, usages: new string[] { "Tool" })]
    [SettingsUITabOrder(
        TAB_GENERAL,
        TAB_ROUNDABOUT,
        TAB_HELIX,
        TAB_SOFTBLOCK,
        TAB_GRID
    )]
    [SettingsUIGroupOrder(GROUP_KEYBINDS, GROUP_DEFAULTS, GROUP_CONTROLS)]
    public class ToolBoxSettings : ModSetting
    {
        public const string TAB_GENERAL = "General";

        public const string TAB_ROUNDABOUT = "Perfect Shape";
        public const string TAB_HELIX = "Procedural Helix";
        public const string TAB_SOFTBLOCK = "Soft Block";
        public const string TAB_GRID = "Smart Grid";

        public const string GROUP_KEYBINDS = "Key Bindings";
        public const string GROUP_DEFAULTS = "Defaults";
        public const string GROUP_CONTROLS = "Controls";

        public const string OpenShapeTool = "OpenShapeTool";
        public const string OpenHelixTool = "OpenHelixTool";
        public const string OpenSoftBlockTool = "OpenSoftBlockTool";
        public const string OpenGridTool = "OpenGridTool";
        public const string UndoToolParameter = "UndoToolParameter";
        public const string RedoToolParameter = "RedoToolParameter";

        private bool m_SuppressCrosswalks = false;
        private bool m_SuppressTrafficLights = false;

        private int m_DefaultShapeDimension = 96;
        private bool m_UseCtrlWheelForShapeDimensionAdjustment = false;

        private int m_DefaultHelixDiameter = 96;
        private float m_DefaultTurns = 3f;
        private float m_DefaultClearance = 9f;
        private bool m_UseCtrlWheelForHelixTurnAdjustment = false;

        private int m_DefaultSoftBlockWidth = 96;
        private int m_DefaultSoftBlockLength = 192;
        private float m_DefaultSoftBlockBorderRadius = 5.0f;
        private bool m_UseCtrlWheelForSoftBlockBorderRadius = false;

        private int m_DefaultBlockWidthU = 6;
        private int m_DefaultBlockLengthU = 6;
        private int m_DefaultColumns = 2;
        private int m_DefaultRows = 2;

        public event Action<int, int> OnToolParametersChanged;
        public event Action OnSuppressCrosswalkChanged;
        public event Action OnSuppressTrafficLightsChanged;
        public ToolBoxSettings(IMod mod) : base(mod)
        {
            SetDefaults();
        }
        // -------------------------
        // General
        // -------------------------
        [SettingsUISection(TAB_GENERAL, GROUP_KEYBINDS)]
        [SettingsUIKeyboardBinding(BindingKeyboard.C, OpenShapeTool, ctrl: true)]
        public ProxyBinding OpenShapeToolKey { get; set; }

        [SettingsUISection(TAB_GENERAL, GROUP_KEYBINDS)]
        [SettingsUIKeyboardBinding(BindingKeyboard.H, OpenHelixTool, ctrl: true)]
        public ProxyBinding OpenHelixToolKey { get; set; }

        [SettingsUISection(TAB_GENERAL, GROUP_KEYBINDS)]
        [SettingsUIKeyboardBinding(BindingKeyboard.S, OpenSoftBlockTool, ctrl: true)]
        public ProxyBinding OpenSoftBlockToolKey { get; set; }

        [SettingsUISection(TAB_GENERAL, GROUP_KEYBINDS)]
        [SettingsUIKeyboardBinding(BindingKeyboard.G, OpenGridTool, ctrl: true)]
        public ProxyBinding OpenGridToolKey { get; set; }

        [SettingsUISection(TAB_GENERAL, GROUP_KEYBINDS)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Z, UndoToolParameter, ctrl: true)]
        public ProxyBinding UndoToolParameterKey { get; set; }

        [SettingsUISection(TAB_GENERAL, GROUP_KEYBINDS)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Y, RedoToolParameter, ctrl: true)]
        public ProxyBinding RedoToolParameterKey { get; set; }

        [SettingsUISection(TAB_GENERAL, GROUP_CONTROLS)]
        public bool SuppressCrosswalks
        {
            get => m_SuppressCrosswalks;
            set
            {
                if (m_SuppressCrosswalks == value) return;

                m_SuppressCrosswalks = value;
                OnSuppressCrosswalkChanged?.Invoke();
            }
        }

        // -------------------------
        // Shape
        // -------------------------
        [SettingsUISection(TAB_ROUNDABOUT, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 48, max = 320, step = 1)]
        public int DefaultShapeDimension
        {
            get => m_DefaultShapeDimension;
            set
            {
                int clamped = Math.Clamp(value, 48, 320);
                if (m_DefaultShapeDimension == clamped) return;

                m_DefaultShapeDimension = clamped;
                OnToolParametersChanged?.Invoke(1, 1);
            }
        }

        [SettingsUISection(TAB_ROUNDABOUT, GROUP_CONTROLS)]
        public bool UseCtrlWheelForShapeDimensionAdjustment
        {
            get => m_UseCtrlWheelForShapeDimensionAdjustment;
            set
            {
                if (m_UseCtrlWheelForShapeDimensionAdjustment == value) return;

                m_UseCtrlWheelForShapeDimensionAdjustment = value;
            }
        }

        // -------------------------
        // Helix
        // -------------------------
        [SettingsUISection(TAB_HELIX, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 48, max = 320, step = 1)]
        public int DefaultHelixDiameter
        {
            get => m_DefaultHelixDiameter;
            set
            {
                int clamped = Math.Clamp(value, 48, 320);
                if (m_DefaultHelixDiameter == clamped) return;

                m_DefaultHelixDiameter = clamped;
                OnToolParametersChanged?.Invoke(2, 1);
            }
        }

        [SettingsUISection(TAB_HELIX, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 1, max = 12, step = 1f)]
        public float DefaultTurns
        {
            get => m_DefaultTurns;
            set
            {
                float clamped = Math.Clamp(value, 1f, 12f);
                if (Math.Abs(m_DefaultTurns - clamped) < 0.0001f) return;

                m_DefaultTurns = clamped;
                OnToolParametersChanged?.Invoke(2, 2);
            }
        }

        [SettingsUISection(TAB_HELIX, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 9, max = 14, step = 1f)]
        public float DefaultClearance
        {
            get => m_DefaultClearance;
            set
            {
                float clamped = Math.Clamp(value, 9f, 14f);
                if (Math.Abs(m_DefaultClearance - clamped) < 0.0001f) return;

                m_DefaultClearance = clamped;
                OnToolParametersChanged?.Invoke(2, 3);
            }
        }

        [SettingsUISection(TAB_HELIX, GROUP_CONTROLS)]
        public bool UseCtrlWheelForHelixTurnAdjustment
        {
            get => m_UseCtrlWheelForHelixTurnAdjustment;
            set
            {
                if (m_UseCtrlWheelForHelixTurnAdjustment == value) return;

                m_UseCtrlWheelForHelixTurnAdjustment = value;
            }
        }

        // -------------------------
        // Soft Block
        // -------------------------
        [SettingsUISection(TAB_SOFTBLOCK, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 48, max = 320, step = 1)]
        public int DefaultSoftBlockWidth
        {
            get => m_DefaultSoftBlockWidth;
            set
            {
                int clamped = Math.Clamp(value, 48, 320);
                if (m_DefaultSoftBlockWidth == clamped) return;

                m_DefaultSoftBlockWidth = clamped;
                OnToolParametersChanged?.Invoke(3, 1);
            }
        }

        [SettingsUISection(TAB_SOFTBLOCK, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 48, max = 320, step = 1)]
        public int DefaultSoftBlockLength
        {
            get => m_DefaultSoftBlockLength;
            set
            {
                int clamped = Math.Clamp(value, 48, 320);
                if (m_DefaultSoftBlockLength == clamped) return;

                m_DefaultSoftBlockLength = clamped;
                OnToolParametersChanged?.Invoke(3, 2);
            }
        }
        [SettingsUISection(TAB_SOFTBLOCK, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 1, max = 10, step = 1f)]
        public float DefaultSoftBlockBorderRadius
        {
            get => m_DefaultSoftBlockBorderRadius;
            set
            {
                float clamped = Math.Clamp(value, 1f, 10f);
                if (m_DefaultSoftBlockBorderRadius == clamped) return;

                m_DefaultSoftBlockBorderRadius = clamped;
                OnToolParametersChanged?.Invoke(3, 3);
            }
        }

        [SettingsUISection(TAB_SOFTBLOCK, GROUP_CONTROLS)]
        public bool UseCtrlWheelForSoftBlockBorderRadius
        {
            get => m_UseCtrlWheelForSoftBlockBorderRadius;
            set
            {
                if (m_UseCtrlWheelForSoftBlockBorderRadius == value) return;

                m_UseCtrlWheelForSoftBlockBorderRadius = value;
            }
        }

        // -------------------------
        // Grid
        // -------------------------
        [SettingsUISection(TAB_GRID, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 1, max = 48, step = 1)]
        public int DefaultBlockWidthU
        {
            get => m_DefaultBlockWidthU;
            set
            {
                int clamped = Math.Clamp(value, 1, 48);
                if (m_DefaultBlockWidthU == clamped) return;

                m_DefaultBlockWidthU = clamped;
                OnToolParametersChanged?.Invoke(4, 1);
            }
        }

        [SettingsUISection(TAB_GRID, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 1, max = 48, step = 1)]
        public int DefaultBlockLengthU
        {
            get => m_DefaultBlockLengthU;
            set
            {
                int clamped = Math.Clamp(value, 1, 48);
                if (m_DefaultBlockLengthU == clamped) return;

                m_DefaultBlockLengthU = clamped;
                OnToolParametersChanged?.Invoke(4, 2);
            }
        }

        [SettingsUISection(TAB_GRID, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 1, max = 24, step = 1)]
        public int DefaultColumns
        {
            get => m_DefaultColumns;
            set
            {
                int clamped = Math.Clamp(value, 1, 24);
                if (m_DefaultColumns == clamped) return;

                m_DefaultColumns = clamped;
                OnToolParametersChanged?.Invoke(4, 3);
            }
        }

        [SettingsUISection(TAB_GRID, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 1, max = 24, step = 1)]
        public int DefaultRows
        {
            get => m_DefaultRows;
            set
            {
                int clamped = Math.Clamp(value, 1, 24);
                if (m_DefaultRows == clamped) return;

                m_DefaultRows = clamped;
                OnToolParametersChanged?.Invoke(4, 4);
            }
        }

        [SettingsUISection(TAB_GRID, GROUP_DEFAULTS)]
        public bool SuppressTrafficLights
        {
            get => m_SuppressTrafficLights;
            set
            {
                if (m_SuppressTrafficLights == value) return;

                m_SuppressTrafficLights = value;
                OnSuppressTrafficLightsChanged?.Invoke();
            }
        }

        public override void SetDefaults()
        {
            m_SuppressCrosswalks = false;
            m_SuppressTrafficLights = false;

            m_DefaultShapeDimension = 96;
            m_UseCtrlWheelForShapeDimensionAdjustment = false;

            m_DefaultHelixDiameter = 96;
            m_DefaultTurns = 3f;
            m_DefaultClearance = 9f;
            m_UseCtrlWheelForHelixTurnAdjustment = false;

            m_DefaultSoftBlockWidth = 96;
            m_DefaultSoftBlockLength = 192;
            m_DefaultSoftBlockBorderRadius = 5;
            m_UseCtrlWheelForSoftBlockBorderRadius = false;

            m_DefaultBlockWidthU = 6;
            m_DefaultBlockLengthU = 6;
            m_DefaultColumns = 2;
            m_DefaultRows = 2;
        }
    }
}