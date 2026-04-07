using System;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace MertsToolBox
{
    [FileLocation("ModsSettings/MertsToolBox/MertsToolBox")]
    [SettingsUITabOrder(
        TAB_CIRCLE,
        TAB_HELIX,
        TAB_SUPERELLIPSE,
        TAB_GRID
    )]
    public class Settings : ModSetting
    {
        public const string TAB_CIRCLE = "Circle";
        public const string TAB_HELIX = "Helix";
        public const string TAB_SUPERELLIPSE = "Super Ellipse";
        public const string TAB_GRID = "Grid";

        public const string GROUP_DEFAULTS = "Defaults";
        public const string GROUP_CONTROLS = "Controls";

        private int m_DefaultCircleDiameter = 96;
        private bool m_UseCtrlWheelForCircleDiameterAdjustment = false;

        private int m_DefaultHelixDiameter = 96;
        private float m_DefaultTurns = 3f;
        private float m_DefaultClearance = 8f;
        private bool m_UseCtrlWheelForHelixTurnAdjustment = false;
        private bool m_EnableHelixSnap = false;

        private int m_DefaultEllipseWidth = 96;
        private int m_DefaultEllipseLength = 192;
        private bool m_UseCtrlWheelForShapeAdjustment = false;

        private int m_BlockWidthU = 6;
        private int m_BlockLengthU = 6;
        private int m_Columns = 2;
        private int m_Rows = 2;
        private bool m_EnableGridSnap = false;

        public static event Action OnOptionsChanged;

        public Settings(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        // -------------------------
        // Circle
        // -------------------------

        [SettingsUISection(TAB_CIRCLE, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 48, max = 320, step = 8)]
        public int DefaultCircleDiameter
        {
            get => m_DefaultCircleDiameter;
            set
            {
                int clamped = Math.Clamp(value, 48, 320);
                if (m_DefaultCircleDiameter == clamped) return;

                m_DefaultCircleDiameter = clamped;
                OnOptionsChanged?.Invoke();
            }
        }

        [SettingsUISection(TAB_CIRCLE, GROUP_CONTROLS)]
        public bool UseCtrlWheelForCircleDiameterAdjustment
        {
            get => m_UseCtrlWheelForCircleDiameterAdjustment;
            set
            {
                if (m_UseCtrlWheelForCircleDiameterAdjustment == value) return;

                m_UseCtrlWheelForCircleDiameterAdjustment = value;
                OnOptionsChanged?.Invoke();
            }
        }

        // -------------------------
        // Helix
        // -------------------------

        [SettingsUISection(TAB_HELIX, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 48, max = 320, step = 8)]
        public int DefaultHelixDiameter
        {
            get => m_DefaultHelixDiameter;
            set
            {
                int clamped = Math.Clamp(value, 48, 320);
                if (m_DefaultHelixDiameter == clamped) return;

                m_DefaultHelixDiameter = clamped;
                OnOptionsChanged?.Invoke();
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
                OnOptionsChanged?.Invoke();
            }
        }

        [SettingsUISection(TAB_HELIX, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 8, max = 14, step = 1f)]
        public float DefaultClearance
        {
            get => m_DefaultClearance;
            set
            {
                float clamped = Math.Clamp(value, 8f, 14f);
                if (Math.Abs(m_DefaultClearance - clamped) < 0.0001f) return;

                m_DefaultClearance = clamped;
                OnOptionsChanged?.Invoke();
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
                OnOptionsChanged?.Invoke();
            }
        }
        [SettingsUISection(TAB_HELIX, GROUP_CONTROLS)]
        public bool EnableHelixSnap
        {
            get => m_EnableHelixSnap;
            set
            {
                if (m_EnableHelixSnap == value) return;

                m_EnableHelixSnap = value;
                OnOptionsChanged?.Invoke();
            }
        }
        // -------------------------
        // Super Ellipse
        // -------------------------

        [SettingsUISection(TAB_SUPERELLIPSE, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 48, max = 320, step = 8)]
        public int DefaultEllipseWidth
        {
            get => m_DefaultEllipseWidth;
            set
            {
                int clamped = Math.Clamp(value, 48, 320);
                if (m_DefaultEllipseWidth == clamped) return;

                m_DefaultEllipseWidth = clamped;
                OnOptionsChanged?.Invoke();
            }
        }

        [SettingsUISection(TAB_SUPERELLIPSE, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 48, max = 320, step = 8)]
        public int DefaultEllipseLength
        {
            get => m_DefaultEllipseLength;
            set
            {
                int clamped = Math.Clamp(value, 48, 320);
                if (m_DefaultEllipseLength == clamped) return;

                m_DefaultEllipseLength = clamped;
                OnOptionsChanged?.Invoke();
            }
        }

        [SettingsUISection(TAB_SUPERELLIPSE, GROUP_CONTROLS)]
        public bool UseCtrlWheelForShapeAdjustment
        {
            get => m_UseCtrlWheelForShapeAdjustment;
            set
            {
                if (m_UseCtrlWheelForShapeAdjustment == value) return;

                m_UseCtrlWheelForShapeAdjustment = value;
                OnOptionsChanged?.Invoke();
            }
        }

        // -------------------------
        // Grid
        // -------------------------

        [SettingsUISection(TAB_GRID, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 1, max = 24, step = 1)]
        public int BlockWidthU
        {
            get => m_BlockWidthU;
            set
            {
                int clamped = Math.Clamp(value, 1, 24);
                if (m_BlockWidthU == clamped) return;

                m_BlockWidthU = clamped;
                OnOptionsChanged?.Invoke();
            }
        }

        [SettingsUISection(TAB_GRID, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 1, max = 24, step = 1)]
        public int BlockLengthU
        {
            get => m_BlockLengthU;
            set
            {
                int clamped = Math.Clamp(value, 1, 24);
                if (m_BlockLengthU == clamped) return;

                m_BlockLengthU = clamped;
                OnOptionsChanged?.Invoke();
            }
        }

        [SettingsUISection(TAB_GRID, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 1, max = 24, step = 1)]
        public int Columns
        {
            get => m_Columns;
            set
            {
                int clamped = Math.Clamp(value, 1, 24);
                if (m_Columns == clamped) return;

                m_Columns = clamped;
                OnOptionsChanged?.Invoke();
            }
        }

        [SettingsUISection(TAB_GRID, GROUP_DEFAULTS)]
        [SettingsUISlider(min = 1, max = 24, step = 1)]
        public int Rows
        {
            get => m_Rows;
            set
            {
                int clamped = Math.Clamp(value, 1, 24);
                if (m_Rows == clamped) return;

                m_Rows = clamped;
                OnOptionsChanged?.Invoke();
            }
        }
        [SettingsUISection(TAB_GRID, GROUP_CONTROLS)]
        public bool EnableGridSnap
        {
            get => m_EnableGridSnap;
            set
            {
                if (m_EnableGridSnap == value) return;

                m_EnableGridSnap = value;
                OnOptionsChanged?.Invoke();
            }
        }
        public override void SetDefaults()
        {
            m_DefaultCircleDiameter = 96;
            m_UseCtrlWheelForCircleDiameterAdjustment = false;

            m_DefaultHelixDiameter = 96;
            m_DefaultTurns = 3f;
            m_DefaultClearance = 8f;
            m_UseCtrlWheelForHelixTurnAdjustment = false;
            m_EnableHelixSnap = false;

            m_DefaultEllipseWidth = 96;
            m_DefaultEllipseLength = 192;
            m_UseCtrlWheelForShapeAdjustment = false;

            m_BlockWidthU = 6;
            m_BlockLengthU = 6;
            m_Columns = 2;
            m_Rows = 2;
            m_EnableGridSnap = false;
        }

        public override void Apply()
        {
            base.Apply();
        }
    }
}