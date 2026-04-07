using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using HarmonyLib;

namespace MertsToolBox
{
    public class Mod : IMod
    {
        #region 1. CORE SYSTEMS & SETTINGS

        private Harmony m_Harmony;
        public static Settings settings;

        #endregion

        #region 2. MOD LIFECYCLE (IMod)

        /// <summary>
        /// Invoked by the game when the mod is loaded. Initializes settings, registers UI/localization, applies Harmony patches, and injects ECS tool systems.
        /// </summary>
        public void OnLoad(UpdateSystem updateSystem)
        {
            settings = new Settings(this);

            AssetDatabase.global.LoadSettings(nameof(Settings), settings, new Settings(this));
            settings.RegisterKeyBindings();
            settings.RegisterInOptionsUI();

            var lm = GameManager.instance.localizationManager;
            lm.AddSource("en-US", new LocaleEN(settings));

            m_Harmony = new Harmony("com.mert.toolbox");
            m_Harmony.PatchAll();

            updateSystem.UpdateAt<MertsToolBox.CircleToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<MertsToolBox.HelixToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<MertsToolBox.SuperEllipseToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<MertsToolBox.GridToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<MertsToolBox.MertToolBoxUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<MertsToolBox.HelixToolErrorFlagSystem>(SystemUpdatePhase.ToolUpdate);
            
            ModRuntime.Log("ToolBox loaded.");
        }

        /// <summary>
        /// Invoked by the game when the mod is disposed. Reverts Harmony patches and safely saves user settings before exit.
        /// </summary>
        public void OnDispose()
        {
            m_Harmony?.UnpatchAll();

            if (settings != null)
            {
                _ = AssetDatabase.global.SaveSettings();

                settings?.ApplyAndSave();
                settings?.UnregisterInOptionsUI();
            }
        }

        #endregion
    }
}