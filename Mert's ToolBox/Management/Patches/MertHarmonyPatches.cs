using Game.Prefabs;
using Game.UI.InGame;
using HarmonyLib;
using System;
using Unity.Entities;

namespace MertsToolBox.Management.Patches
{
    [HarmonyPatch(typeof(ToolbarUISystem), "SelectAsset", new[] { typeof(Entity), typeof(bool) })]
    public static class SelectAsset_CustomToolAbortPatch
    {
        public static void Prefix(Entity assetEntity, bool updateTool)
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
    }
    [HarmonyPatch(typeof(ToolbarUISystem), "SelectAssetCategory")]
    public static class SelectCategory_ControlledReplayPatch
    {
        public static bool Prefix(ToolbarUISystem __instance, Entity assetCategory)
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

            if (!MertToolbarHandoffMemory.TryResolveEntity(
                    oldRoad,
                    out Entity oldRoadEntity))
                return true;

            MertToolState.ActiveTool?.RequestDisable(
                ToolExitMode.VanillaToolbarClear);

            MertToolbarReflection.ReplaySelectAsset(
                __instance,
                oldRoadEntity,
                true);

            MertToolbarReflection.ReplaySelectAssetCategory(
                __instance,
                assetCategory);

            return false;
        }
    }
    [HarmonyPatch(typeof(ToolbarUISystem), "ClearAssetSelection", new Type[] { })]
    public static class ClearAssetSelection_ControlledReplayPatch
    {
        public static bool Prefix(ToolbarUISystem __instance)
        {
            return MertToolbarClearController.TryHandleClearSelection(__instance, null);
        }
    }
    [HarmonyPatch(typeof(ToolbarUISystem), "ClearAssetSelection", new[] { typeof(bool) })]
    public static class ClearAssetSelection_bool_ControlledReplayPatch
    {
        public static bool Prefix(ToolbarUISystem __instance, bool updateTool)
        {
            return MertToolbarClearController.TryHandleClearSelection(__instance, updateTool);
        }
    }
}