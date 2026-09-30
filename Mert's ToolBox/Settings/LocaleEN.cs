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
                // --- Tabs ---
                { m_Settings.GetOptionTabLocaleID(ToolBoxSettings.TAB_GENERAL), "Key Bindings" },
                { m_Settings.GetOptionTabLocaleID(ToolBoxSettings.TAB_ROUNDABOUT), "Perfect Shape" },
                { m_Settings.GetOptionTabLocaleID(ToolBoxSettings.TAB_HELIX), "Procedural Helix" },
                { m_Settings.GetOptionTabLocaleID(ToolBoxSettings.TAB_SOFTBLOCK), "Soft Block" },
                { m_Settings.GetOptionTabLocaleID(ToolBoxSettings.TAB_GRID), "Smart Grid" },

                // --- Groups ---
                { m_Settings.GetOptionGroupLocaleID(ToolBoxSettings.GROUP_KEYBINDS), "Global Shortcuts" },
                { m_Settings.GetOptionGroupLocaleID(ToolBoxSettings.GROUP_DEFAULTS), "Defaults" },
                { m_Settings.GetOptionGroupLocaleID(ToolBoxSettings.GROUP_CONTROLS), "Controls" },

                // --- Key Bindings Map (Genel Başlık) ---
                { m_Settings.GetBindingMapLocaleID(), "Toolbox Controls" },

                // --- Individual Key Bindings ---
                { m_Settings.GetBindingKeyLocaleID(ToolBoxSettings.OpenShapeTool), "Open Perfect Shape" },
                { m_Settings.GetBindingKeyLocaleID(ToolBoxSettings.OpenHelixTool), "Open Procedural Helix" },
                { m_Settings.GetBindingKeyLocaleID(ToolBoxSettings.OpenSoftBlockTool), "Open Soft Block" },
                { m_Settings.GetBindingKeyLocaleID(ToolBoxSettings.OpenGridTool), "Open Smart Grid" },
                { m_Settings.GetBindingKeyLocaleID(ToolBoxSettings.UndoToolParameter), "Undo Parameter Change" },
                { m_Settings.GetBindingKeyLocaleID(ToolBoxSettings.RedoToolParameter), "Redo Parameter Change" },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.OpenShapeToolKey)), "Open Perfect Shape" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.OpenShapeToolKey)), "Toggles the Perfect Shape tool on or off." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.OpenHelixToolKey)), "Open Procedural Helix" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.OpenHelixToolKey)), "Toggles the Procedural Helix tool on or off." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.OpenSoftBlockToolKey)), "Open Soft Block" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.OpenSoftBlockToolKey)), "Toggles the Soft Block tool on or off." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.OpenGridToolKey)), "Open Smart Grid" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.OpenGridToolKey)), "Toggles the Smart Grid tool on or off." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.UndoToolParameterKey)), "Undo Parameter Change" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.UndoToolParameterKey)), "Reverts the last adjustment made to the active tool's parameters." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.RedoToolParameterKey)), "Redo Parameter Change" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.RedoToolParameterKey)), "Restores the previously undone parameter adjustment." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.SuppressCrosswalks)), "Remove Crosswalks (Zebra Crossing for British :)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.SuppressCrosswalks)), "Sets crosswalks (zebra crossings) removal as the default behavior on shape placement." },

                 { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.SuppressTrafficLights)), "Remove Traffic Lights" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.SuppressTrafficLights)), "Sets traffic lights removal as the default behavior on shape placement." },

                // -------------------------
                // Shape
                // -------------------------
                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultShapeDimension)), "Default Shape Dimension (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultShapeDimension)), "Sets the starting dimension used when the Shape tool is opened." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.UseCtrlWheelForShapeDimensionAdjustment)), "Use Ctrl+Wheel for Dimension adjustment" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.UseCtrlWheelForShapeDimensionAdjustment)), "Allows the Shape tool dimension to be adjusted with Ctrl+Mouse Wheel. Recommended to turn this off if it conflicts with another binding." },

                // -------------------------
                // Helix
                // -------------------------
                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultHelixDiameter)), "Default Helix Diameter (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultHelixDiameter)), "Sets the starting diameter used when the Helix tool is opened." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultTurns)), "Default Turns" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultTurns)), "Sets the default number of full turns used by the Helix tool." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultClearance)), "Default Clearance (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultClearance)), "Sets the default vertical clearance between helix levels." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.UseCtrlWheelForHelixTurnAdjustment)), "Use Ctrl+Wheel for Turn adjustment" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.UseCtrlWheelForHelixTurnAdjustment)), "Allows the Helix tool turns to be adjusted with Ctrl+Mouse Wheel. Recommended to turn this off if it conflicts with another binding." },

                // -------------------------
                // Soft Block
                // -------------------------
                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultSoftBlockWidth)), "Default Shape Width (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultSoftBlockWidth)), "Sets the starting width used when the Soft Block tool is opened." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultSoftBlockLength)), "Default Shape Length (m)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultSoftBlockLength)), "Sets the starting length used when the Soft Block tool is opened." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.UseCtrlWheelForSoftBlockBorderRadius)), "Use Ctrl+Wheel for border radius adjustment" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.UseCtrlWheelForSoftBlockBorderRadius)), "Allows the Soft Block border radius to be adjusted with Ctrl+Mouse Wheel. Recommended to turn this off if it conflicts with another binding." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultSoftBlockBorderRadius)), "Default border radius (R)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultSoftBlockBorderRadius)), "Sets the border radius when the Soft Block tool is opened." },

                // -------------------------
                // Grid
                // -------------------------
                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultBlockWidthU)), "Block Width (U)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultBlockWidthU)), "Sets the default block width used by the Grid tool, measured in cell units." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultBlockLengthU)), "Block Depth (U)" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultBlockLengthU)), "Sets the default block depth used by the Grid tool, measured in cell units." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultColumns)), "Columns" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultColumns)), "Sets the default number of columns used by the Grid tool." },

                { m_Settings.GetOptionLabelLocaleID(nameof(ToolBoxSettings.DefaultRows)), "Rows" },
                { m_Settings.GetOptionDescLocaleID(nameof(ToolBoxSettings.DefaultRows)), "Sets the default number of rows used by the Grid tool." },
            };
        }

        public void Unload()
        {
        }
    }
}