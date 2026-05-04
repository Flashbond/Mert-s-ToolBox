using Colossal;
using System.Collections.Generic;

namespace MertsToolBox.Settings
{
    public class LocaleEN : IDictionarySource
    {
        private readonly ToolBoxSettings m_Settings;

        public LocaleEN(ToolBoxSettings settings)
        {
            m_Settings = settings;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Settings.GetSettingsLocaleID(), "Mert's ToolBox" },

                // Tabs
                { m_Settings.GetOptionTabLocaleID(ToolBoxSettings.TAB_CIRCLE), "Perfect Circle" },
                { m_Settings.GetOptionTabLocaleID(ToolBoxSettings.TAB_HELIX), "Procedural Helix" },
                { m_Settings.GetOptionTabLocaleID(ToolBoxSettings.TAB_SUPERELLIPSE), "SuperEllipse" },
                { m_Settings.GetOptionTabLocaleID(ToolBoxSettings.TAB_GRID), "Smart Grid" },

                // Fallback tab keys
                { "Settings.TAB[Circle]", "Perfect Circle" },
                { "Settings.TAB[Helix]", "Procedural Helix" },
                { "Settings.TAB[Super Ellipse]", "SuperEllipse" },
                { "Settings.TAB[Grid]", "Smart Grid" },

                // Groups
                { "Settings.SECTION[Defaults]", "Defaults" },
                { "Settings.SECTION[Controls]", "Controls" },

                // -------------------------
                // Circle
                // -------------------------
                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultCircleDiameter)), "Default Circle Diameter (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultCircleDiameter)), "Sets the starting diameter used when the Circle tool is opened." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.UseCtrlWheelForCircleDiameterAdjustment)), "Use Ctrl+Wheel for Circle Diameter Adjustment" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.UseCtrlWheelForCircleDiameterAdjustment)), "Allows the Circle tool diameter to be adjusted with Ctrl+Mouse Wheel. Recommended to turn this off if it conflicts with another binding." },

                // -------------------------
                // Helix
                // -------------------------
                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultHelixDiameter)), "Default Helix Diameter (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultHelixDiameter)), "Sets the starting diameter used when the Helix tool is opened." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultTurns)), "Default Turns" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultTurns)), "Sets the default number of full turns used by the Helix tool." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultClearance)), "Default Clearance (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultClearance)), "Sets the default vertical clearance between helix levels." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.UseCtrlWheelForHelixTurnAdjustment)), "Use Ctrl+Wheel for Helix Diameter Adjustment" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.UseCtrlWheelForHelixTurnAdjustment)), "Allows the Helix tool diameter to be adjusted with Ctrl+Mouse Wheel. Recommended to turn this off if it conflicts with another binding." },
               
                // -------------------------
                // Super Ellipse
                // -------------------------
                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultEllipseWidth)), "Default Shape Width (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultEllipseWidth)), "Sets the starting width used when the Super Ellipse tool is opened." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultEllipseLength)), "Default Shape Length (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultEllipseLength)), "Sets the starting length used when the Super Ellipse tool is opened." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.UseCtrlWheelForShapeAdjustment)), "Use Ctrl+Wheel for Shape Adjustment" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.UseCtrlWheelForShapeAdjustment)), "Allows the Super Ellipse shape parameter to be adjusted with Ctrl+Mouse Wheel. Recommended to turn this off if it conflicts with another binding." },

                // -------------------------
                // Grid
                // -------------------------
                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.BlockWidthU)), "Block Width (U)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.BlockWidthU)), "Sets the default block width used by the Grid tool, measured in cell units." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.BlockLengthU)), "Block Depth (U)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.BlockLengthU)), "Sets the default block depth used by the Grid tool, measured in cell units." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.Columns)), "Columns" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.Columns)), "Sets the default number of columns used by the Grid tool." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.Rows)), "Rows" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.Rows)), "Sets the default number of rows used by the Grid tool." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.EnableGridSnap)), "Enable Grid Snap" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.EnableGridSnap)), "Enables snap functionality for Grid tool." },
            };
        }

        public void Unload()
        {
        }
    }
}