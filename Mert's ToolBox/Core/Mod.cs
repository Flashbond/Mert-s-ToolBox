using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Tools;
using HarmonyLib;
using MertsToolBox.Settings;
using MertsToolBox.Systems;
using MertsToolBox.Systems.SubSystems;

namespace MertsToolBox.Core
{
    public class Mod : IMod
    {
        #region 1. CORE SYSTEMS & SETTINGS
        private Harmony m_Harmony;
        public static ToolBoxSettings settings;
        #endregion

        #region 2. MOD LIFECYCLE (IMod)

        /// <summary>
        /// Invoked by the game when the mod is loaded. Initializes settings, registers UI/localization, applies Harmony patches, and injects ECS tool systems.
        /// </summary>
        public void OnLoad(UpdateSystem updateSystem)
        {
            settings = new ToolBoxSettings(this);

            settings.RegisterInOptionsUI();
            settings.RegisterKeyBindings();

            AssetDatabase.global.LoadSettings(nameof(Settings), settings, new ToolBoxSettings(this));

            var lm = GameManager.instance.localizationManager;
            lm.AddSource("en-US", new LocaleEN(settings));

            m_Harmony = new Harmony("com.mertstoolbox");
            m_Harmony.PatchAll();

            updateSystem.UpdateAt<ShapeToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<HelixToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<SoftBlockToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<GridToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAfter<MertToolBoxTerrainFlattenSystem, ObjectToolSystem>(SystemUpdatePhase.ToolUpdate);
      
    
            updateSystem.UpdateAt<MertToolBoxUISystem>(SystemUpdatePhase.UIUpdate);
        
            updateSystem.UpdateBefore<MertPlacementGhostSuppressSystem, GenerateObjectsSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateBefore<MertFlattenCourseSystem, GenerateObjectsSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAt<MertTrafficLightSuppressSystem>(SystemUpdatePhase.Modification2);
            updateSystem.UpdateAt<InjectDummyParentToPillarsSystem>(SystemUpdatePhase.Modification3);
            updateSystem.UpdateAfter<MertPillarRoadStopSystem, Game.Objects.AlignSystem>(SystemUpdatePhase.Modification4);
            updateSystem.UpdateBefore<MertPillarValidationPrepareSystem, ValidationSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<MertPillarValidationRestoreSystem, ValidationSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateBefore<MertHelixElevationPrepareSystem, ValidationSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAfter<MertHelixElevationRestoreSystem, ValidationSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAt<MertFlattenLatePreviewSystem>(SystemUpdatePhase.ModificationEnd);

            ModRuntime.Log("ToolBox loaded.");
        }

        /// <summary>
        /// Invoked by the game when the mod is disposed. Reverts Harmony patches and safely saves user settings before exit.
        /// </summary>
        public void OnDispose()
        {
            m_Harmony?.UnpatchAll("com.mertstoolbox");
            settings?.UnregisterInOptionsUI();
            settings = null;
        }
        #endregion
    }
}