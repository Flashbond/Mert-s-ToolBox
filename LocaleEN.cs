using Colossal;
using System.Collections.Generic;

namespace MertsToolBox
{
    public class LocaleEN : IDictionarySource
    {
        private readonly Settings m_Settings;

        public LocaleEN(Settings settings)
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
                { m_Settings.GetOptionTabLocaleID(Settings.TAB_CIRCLE), "Circle" },
                { m_Settings.GetOptionTabLocaleID(Settings.TAB_HELIX), "Helix" },
                { m_Settings.GetOptionTabLocaleID(Settings.TAB_SUPERELLIPSE), "Super Ellipse" },
                { m_Settings.GetOptionTabLocaleID(Settings.TAB_GRID), "Grid" },

                // Fallback tab keys
                { "Settings.TAB[Circle]", "Circle" },
                { "Settings.TAB[Helix]", "Helix" },
                { "Settings.TAB[Super Ellipse]", "Super Ellipse" },
                { "Settings.TAB[Grid]", "Grid" },

                // Groups
                { "Settings.SECTION[Defaults]", "Defaults" },
                { "Settings.SECTION[Controls]", "Controls" },

                // -------------------------
                // Circle
                // -------------------------
                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.DefaultCircleDiameter)), "Default Circle Diameter (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.DefaultCircleDiameter)), "Sets the starting diameter used when the Circle tool is opened." },

                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.UseCtrlWheelForCircleDiameterAdjustment)), "Use Ctrl+Wheel for Circle Diameter Adjustment" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.UseCtrlWheelForCircleDiameterAdjustment)), "Allows the Circle tool diameter to be adjusted with Ctrl+Mouse Wheel. Recommended to turn this off if it conflicts with another binding." },

                // -------------------------
                // Helix
                // -------------------------
                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.DefaultHelixDiameter)), "Default Helix Diameter (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.DefaultHelixDiameter)), "Sets the starting diameter used when the Helix tool is opened." },

                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.DefaultTurns)), "Default Turns" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.DefaultTurns)), "Sets the default number of full turns used by the Helix tool." },

                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.DefaultClearance)), "Default Clearance (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.DefaultClearance)), "Sets the default vertical clearance between helix levels." },

                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.UseCtrlWheelForHelixTurnAdjustment)), "Use Ctrl+Wheel for Helix Diameter Adjustment" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.UseCtrlWheelForHelixTurnAdjustment)), "Allows the Helix tool diameter to be adjusted with Ctrl+Mouse Wheel. Recommended to turn this off if it conflicts with another binding." },

                // -------------------------
                // Super Ellipse
                // -------------------------
                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.DefaultEllipseWidth)), "Default Shape Width (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.DefaultEllipseWidth)), "Sets the starting width used when the Super Ellipse tool is opened." },

                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.DefaultEllipseLength)), "Default Shape Length (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.DefaultEllipseLength)), "Sets the starting length used when the Super Ellipse tool is opened." },

                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.UseCtrlWheelForShapeAdjustment)), "Use Ctrl+Wheel for Shape Adjustment" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.UseCtrlWheelForShapeAdjustment)), "Allows the Super Ellipse shape parameter to be adjusted with Ctrl+Mouse Wheel. Recommended to turn this off if it conflicts with another binding." },

                // -------------------------
                // Grid
                // -------------------------
                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.BlockWidthU)), "Block Width (U)" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.BlockWidthU)), "Sets the default block width used by the Grid tool, measured in cell units." },

                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.BlockLengthU)), "Block Depth (U)" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.BlockLengthU)), "Sets the default block depth used by the Grid tool, measured in cell units." },

                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.Columns)), "Columns" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.Columns)), "Sets the default number of columns used by the Grid tool." },

                { m_Settings.GetOptionLabelLocaleID(nameof(Settings.Rows)), "Rows" },
                { m_Settings.GetOptionDescLocaleID(nameof(Settings.Rows)), "Sets the default number of rows used by the Grid tool." },
            };
        }

        public void Unload()
        {
        }
    }
}