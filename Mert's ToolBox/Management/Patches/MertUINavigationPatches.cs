using Game.Prefabs;
using Game.UI.InGame;
using HarmonyLib;
using MertsToolBox.Core;
using System;
using Unity.Entities;

namespace MertsToolBox.Management.Patches
{
    /// <summary>
    /// Shared safety for every Harmony patch of the mod: Prepare() skips a patch (instead of failing the whole mod)
    /// when a game update removed its target, and a patch that throws disables itself after logging once, falling
    /// back to vanilla.
    /// </summary>
    internal static class MertPatchGuard
    {
        /// <summary>True when the target method exists; logs once and returns false otherwise.</summary>
        internal static bool TargetExists(string patchName, Type type, string methodName, Type[] parameters)
        {
            try
            {
                bool found = (parameters == null
                    ? AccessTools.Method(type, methodName)
                    : AccessTools.Method(type, methodName, parameters)) != null;
                if (!found)
                    ModRuntime.Warn($"{patchName}: {type.Name}.{methodName} not found; patch skipped.");
                return found;
            }
            catch (Exception)
            {
                // Ambiguous lookup (e.g. a new overload): leave the decision to Harmony's own target resolution.
                return true;
            }
        }

        /// <summary>Logs the first failure of a patch; the caller then stops running its body.</summary>
        internal static void ReportFailure(string patchName, Exception e)
        {
            ModRuntime.Warn($"{patchName} disabled after an error: {e}");
        }
    }

    [HarmonyPatch(typeof(ToolbarUISystem), "SelectAsset", new[] { typeof(Entity), typeof(bool) })]
    public static class SelectAsset_CustomToolAbortPatch
    {
        private static bool s_Failed;

        public static bool Prepare() =>
            MertPatchGuard.TargetExists(nameof(SelectAsset_CustomToolAbortPatch), typeof(ToolbarUISystem), "SelectAsset", new[] { typeof(Entity), typeof(bool) });

        public static void Prefix(Entity assetEntity, bool updateTool)
        {
            if (s_Failed)
                return;

            try
            {
                if (MertToolState.ControlledSelectAssetReplay)
                    return;

                if (!MertToolbarHandoffMemory.IsAnyCustomToolOpen())
                    return;

                if (assetEntity == Entity.Null)
                    return;

                if (!MertToolbarHandoffMemory.IsSupportedNetPrefab(assetEntity, out _))
                    return;

                MertToolState.ActiveTool?.RequestDisable(ToolExitMode.VanillaToolbarClear);
            }
            catch (Exception e)
            {
                s_Failed = true;
                MertPatchGuard.ReportFailure(nameof(SelectAsset_CustomToolAbortPatch), e);
            }
        }
    }

    [HarmonyPatch(typeof(ToolbarUISystem), "SelectAssetCategory")]
    public static class SelectCategory_ControlledReplayPatch
    {
        private static bool s_Failed;

        public static bool Prepare() =>
            MertPatchGuard.TargetExists(nameof(SelectCategory_ControlledReplayPatch), typeof(ToolbarUISystem), "SelectAssetCategory", null);

        public static bool Prefix(ToolbarUISystem __instance, Entity assetCategory)
        {
            if (s_Failed)
                return true;

            try
            {
                if (MertToolState.ControlledSelectCategoryReplay)
                    return true;

                if (MertToolState.ControlledSelectAssetReplay)
                    return true;

                if (!MertToolbarHandoffMemory.IsAnyCustomToolOpen())
                    return true;

                if (assetCategory == Entity.Null)
                    return true;

                NetPrefab oldRoad = MertToolState.LaunchRoadPrefab;
                if (oldRoad == null)
                    return true;

                if (!MertToolbarHandoffMemory.TryResolveEntity(oldRoad, out Entity oldRoadEntity))
                    return true;

                MertToolState.ActiveTool?.RequestDisable(ToolExitMode.VanillaToolbarClear);

                MertToolbarReflection.ReplaySelectAsset(__instance, oldRoadEntity, true);
                MertToolbarReflection.ReplaySelectAssetCategory(__instance, assetCategory);

                return false;
            }
            catch (Exception e)
            {
                s_Failed = true;
                MertPatchGuard.ReportFailure(nameof(SelectCategory_ControlledReplayPatch), e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(ToolbarUISystem), "ClearAssetSelection", new Type[] { })]
    public static class ClearAssetSelection_ControlledReplayPatch
    {
        private static bool s_Failed;

        public static bool Prepare() =>
            MertPatchGuard.TargetExists(nameof(ClearAssetSelection_ControlledReplayPatch), typeof(ToolbarUISystem), "ClearAssetSelection", Type.EmptyTypes);

        public static bool Prefix(ToolbarUISystem __instance)
        {
            if (s_Failed)
                return true;

            try
            {
                return MertToolbarClearController.TryHandleClearSelection(__instance, null);
            }
            catch (Exception e)
            {
                s_Failed = true;
                MertPatchGuard.ReportFailure(nameof(ClearAssetSelection_ControlledReplayPatch), e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(ToolbarUISystem), "ClearAssetSelection", new[] { typeof(bool) })]
    public static class ClearAssetSelection_bool_ControlledReplayPatch
    {
        private static bool s_Failed;

        public static bool Prepare() =>
            MertPatchGuard.TargetExists(nameof(ClearAssetSelection_bool_ControlledReplayPatch), typeof(ToolbarUISystem), "ClearAssetSelection", new[] { typeof(bool) });

        public static bool Prefix(ToolbarUISystem __instance, bool updateTool)
        {
            if (s_Failed)
                return true;

            try
            {
                return MertToolbarClearController.TryHandleClearSelection(__instance, updateTool);
            }
            catch (Exception e)
            {
                s_Failed = true;
                MertPatchGuard.ReportFailure(nameof(ClearAssetSelection_bool_ControlledReplayPatch), e);
                return true;
            }
        }
    }
}