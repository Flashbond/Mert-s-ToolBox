using System;
using System.Reflection;
using Colossal.IO.AssetDatabase;
using MertsToolBox.Core;
using Unity.Entities;

namespace MertsToolBox.UI
{
    /// <summary>The two ways a helix can be built: our own validation fixes, or Anarchy.</summary>
    public enum HelixBuildMode
    {
        Toolbox = 0,
        Anarchy = 1,
    }

    /// <summary>
    /// "Build Option" of the helix tool. While the helix tool is open it drives Anarchy's on/off state from our radio
    /// buttons, and when the tool closes it puts Anarchy back to exactly the state the player had before.
    /// Anarchy is reached only through reflection: there is no compile-time reference, so the mod works (and this
    /// class just reports "not installed") when Anarchy is absent or its internals change in an update.
    ///
    /// Anarchy counts as installed only when BOTH are true:
    ///  - its code is loaded and alive (reflection on its UI system and mod instance),
    ///  - its UI module is registered in the asset database (the same list the game's ModManager uses).
    /// Uninstalling a mod in-game removes its UI module but cannot unload its code, so the code check alone would lie.
    /// </summary>
    public static class MertHelixBuildOption
    {
        private const string k_UISystemType = "Anarchy.Systems.Common.AnarchyUISystem";
        private const string k_ModType = "Anarchy.AnarchyMod";
        private const string k_ModName = "Anarchy";
        private const string k_PdxModId = "74604";
        private const BindingFlags k_Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags k_Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Reflected members of one loaded copy of Anarchy.</summary>
        private sealed class AnarchyApi
        {
            public Type SystemType;
            public PropertyInfo Enabled;
            public MethodInfo Toggle;
            public FieldInfo DisableWhenCompleted;
            public PropertyInfo ModInstance;
            public PropertyInfo ModSettings;
        }

        private static AnarchyApi s_Api;
        private static bool s_Broken;
        private static bool s_Searched;

        private static bool s_SessionActive;
        private static bool s_PlayerState;
        private static bool s_UiModulePresent;

        /// <summary>The currently selected build option.</summary>
        public static HelixBuildMode Current { get; private set; } = HelixBuildMode.Toolbox;

        /// <summary>True when Anarchy's code is alive and its UI module is registered. Drives the Anarchy icon.</summary>
        public static bool IsAnarchyInstalled => s_UiModulePresent && GetSystem() != null;

        /// <summary>
        /// True while the helix tool is open with the Anarchy option selected. Our helix validation fixes stand down
        /// then; tagging of the new helix roads still runs so later edits around them keep working.
        /// </summary>
        public static bool AnarchyBuildActive => s_SessionActive && Current == HelixBuildMode.Anarchy;

        /// <summary>Call when the helix tool becomes active. Saves the player's Anarchy state and applies the default option.</summary>
        public static void OnHelixOpened()
        {
            if (s_SessionActive)
                return;

            // Re-resolve every session: an in-game reinstall loads a second copy of Anarchy next to the dead one.
            s_Api = null;
            s_Searched = false;
            s_Broken = false;

            // The UI module list only changes when mods are added/removed, which also needs a trip to the menu:
            // checking once per helix session is enough (and keeps the per-frame UI poll cheap).
            s_UiModulePresent = IsAnarchyUiModuleRegistered();

            object system = GetSystem();
            // A pending "Anarchic bulldozer" auto-enable is not the player's own choice: their real state is off.
            s_PlayerState = system != null && GetEnabled(system) && !GetDisableWhenCompleted(system);
            s_SessionActive = true;
            // Anarchy is the default when installed. A leftover, UI-less Anarchy gets switched off by Toolbox.
            Apply(IsAnarchyInstalled ? HelixBuildMode.Anarchy : HelixBuildMode.Toolbox);
        }

        /// <summary>Call from the radio buttons. The Anarchy option is ignored when Anarchy is not installed.</summary>
        public static void Select(HelixBuildMode mode) => Apply(mode);

        /// <summary>
        /// Call every frame while the helix tool is open. If the player flips Anarchy with its own hotkey or button,
        /// the radio follows instead of fighting them.
        /// </summary>
        public static void Update()
        {
            if (!s_SessionActive)
                return;

            object system = GetSystem();
            if (system == null || !s_UiModulePresent)
            {
                Current = HelixBuildMode.Toolbox;
                return;
            }

            HelixBuildMode actual = GetEnabled(system) ? HelixBuildMode.Anarchy : HelixBuildMode.Toolbox;
            if (actual != Current)
                Current = actual;
        }

        /// <summary>Call when the helix tool is deactivated (and from OnDestroy). Puts Anarchy back to the player's state.</summary>
        public static void OnHelixClosed()
        {
            if (!s_SessionActive)
                return;

            s_SessionActive = false;
            object system = GetSystem();
            if (system != null)
                SetEnabled(system, s_PlayerState);
        }

        /// <summary>Selects a mode and, during a helix session, switches Anarchy to match.</summary>
        private static void Apply(HelixBuildMode mode)
        {
            object system = GetSystem();
            if (mode == HelixBuildMode.Anarchy && (system == null || !s_UiModulePresent))
                mode = HelixBuildMode.Toolbox;

            Current = mode;
            if (s_SessionActive && system != null)
                SetEnabled(system, mode == HelixBuildMode.Anarchy);
        }

        /// <summary>
        /// True when Anarchy's UI module is registered in the asset database: matched by name, or by its Paradox Mods
        /// folder (".../pdx_mods/74604_&lt;version&gt;/..."). If the database cannot be read we do not block Anarchy.
        /// </summary>
        private static bool IsAnarchyUiModuleRegistered()
        {
            try
            {
                foreach (UIModuleAsset module in AssetDatabase.global.GetAssets<UIModuleAsset>(default(SearchFilter<UIModuleAsset>)))
                {
                    if (module == null)
                        continue;

                    string name = module.name;
                    string path = module.path;
                    bool byName = string.Equals(name, k_ModName, StringComparison.OrdinalIgnoreCase);
                    bool byPath = path != null
                                  && (path.IndexOf("\\" + k_PdxModId + "_", StringComparison.Ordinal) >= 0
                                      || path.IndexOf("/" + k_PdxModId + "_", StringComparison.Ordinal) >= 0);

                    if (byName || byPath)
                        return true;
                }

                return false;
            }
            catch (Exception e)
            {
                ModRuntime.Warn($"[BuildOption] Could not read UI modules, assuming Anarchy UI is present: {e.GetType().Name}: {e.Message}");
                return true;
            }
        }

        /// <summary>Anarchy's UI system instance in the game world, or null when Anarchy is absent or unusable.</summary>
        private static object GetSystem()
        {
            if (s_Broken || !Resolve())
                return null;

            // Uninstalling a mod in-game cannot unload its code: the assembly and its systems stay until restart.
            // Anarchy's OnDispose clears its settings though, after which it no longer works; treat that as absent.
            if (!IsModAlive(s_Api))
                return null;

            try
            {
                World world = World.DefaultGameObjectInjectionWorld;
                return world != null && world.IsCreated ? world.GetExistingSystemManaged(s_Api.SystemType) : null;
            }
            catch (Exception e)
            {
                Fail("lookup", e);
                return null;
            }
        }

        /// <summary>
        /// Finds Anarchy's UI system type and its members. When several copies are loaded (in-game reinstall), the
        /// newest living one wins. A missing type means Anarchy is not installed.
        /// </summary>
        private static bool Resolve()
        {
            if (s_Api != null)
                return true;

            // The UI polls this every frame: without Anarchy, do not scan all assemblies each time.
            if (s_Searched)
                return false;
            s_Searched = true;

            AnarchyApi best = null;
            bool bestAlive = false;
            bool apiChanged = false;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try
                {
                    type = assembly.GetType(k_UISystemType, false);
                }
                catch
                {
                    type = null;
                }

                if (type == null)
                    continue;

                AnarchyApi api = BuildApi(type);
                if (api == null)
                {
                    apiChanged = true;
                    continue;
                }

                // Assemblies are listed in load order: a later living copy replaces an earlier one.
                bool alive = IsModAlive(api);
                if (best == null || alive || !bestAlive)
                {
                    best = api;
                    bestAlive = alive;
                }
            }

            if (best == null)
            {
                if (apiChanged)
                    ModRuntime.Warn("[BuildOption] Anarchy found but its toggle API has changed; the Anarchy option is disabled.");
                return false;
            }

            s_Api = best;
            return true;
        }

        /// <summary>Reflects the members we need from one copy of Anarchy, or null when its toggle API has changed.</summary>
        private static AnarchyApi BuildApi(Type type)
        {
            PropertyInfo enabled = type.GetProperty("AnarchyEnabled", k_Instance);
            MethodInfo toggle = type.GetMethod("AnarchyToggled", k_Instance, null, Type.EmptyTypes, null);
            if (enabled == null || enabled.PropertyType != typeof(bool) || toggle == null)
                return null;

            var api = new AnarchyApi { SystemType = type, Enabled = enabled, Toggle = toggle };

            // Optional: only used to tell the player's own state apart from Anarchy's bulldozer auto-enable.
            FieldInfo disable = type.GetField("m_DisableAnarchyWhenCompleted", k_Instance);
            if (disable != null && disable.FieldType == typeof(bool))
                api.DisableWhenCompleted = disable;

            // Optional: used to notice that this copy of Anarchy was uninstalled during this game session.
            Type modType = type.Assembly.GetType(k_ModType, false);
            if (modType != null)
            {
                api.ModInstance = modType.GetProperty("Instance", k_Static);
                api.ModSettings = modType.GetProperty("Settings", k_Instance);
            }

            return api;
        }

        /// <summary>
        /// False once a copy of Anarchy has been disposed (its mod instance or settings are gone). Unknown layouts count
        /// as alive, so a future Anarchy version never disables the option by mistake.
        /// </summary>
        private static bool IsModAlive(AnarchyApi api)
        {
            if (api?.ModInstance == null)
                return true;

            try
            {
                object mod = api.ModInstance.GetValue(null);
                if (mod == null)
                    return false;
                return api.ModSettings == null || api.ModSettings.GetValue(mod) != null;
            }
            catch
            {
                return true;
            }
        }

        private static bool GetEnabled(object system)
        {
            try
            {
                return (bool)s_Api.Enabled.GetValue(system);
            }
            catch (Exception e)
            {
                Fail("read", e);
                return false;
            }
        }

        private static bool GetDisableWhenCompleted(object system)
        {
            try
            {
                return s_Api?.DisableWhenCompleted != null && (bool)s_Api.DisableWhenCompleted.GetValue(system);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Sets Anarchy on or off, toggling only when the state differs.</summary>
        private static void SetEnabled(object system, bool enabled)
        {
            try
            {
                // We own the state now: stop a pending bulldozer auto-disable from flipping it behind our back.
                if (GetDisableWhenCompleted(system))
                    s_Api.DisableWhenCompleted.SetValue(system, false);

                if (GetEnabled(system) != enabled)
                    s_Api.Toggle.Invoke(system, null);
            }
            catch (Exception e)
            {
                Fail("toggle", e);
            }
        }

        private static void Fail(string step, Exception e)
        {
            s_Broken = true;
            Current = HelixBuildMode.Toolbox;
            ModRuntime.Warn($"[BuildOption] Anarchy {step} failed, falling back to Toolbox: {e.GetType().Name}: {e.Message}");
        }
    }
}
