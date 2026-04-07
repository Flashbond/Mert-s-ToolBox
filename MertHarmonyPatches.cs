using Game.UI.InGame;
using HarmonyLib;
using System.Collections.Generic;
using Unity.Entities;

namespace MertsToolBox
{

    [HarmonyPatch(typeof(ToolbarUISystem), "SelectAssetCategory")]
    public static class ToolbarUISystem_SelectAssetCategory_HandoffPatch
    {
        public static void Prefix(ToolbarUISystem __instance, Entity assetCategory)
        {
            if (MertToolState.SuppressToolbarCaptureDuringColdstart)
                return;
            if (assetCategory == Entity.Null)
                return;

            if (MertToolState.SuppressCategoryCapture)
                return;

            if (MertToolState.SuppressUiAbortDuringRestore)
                return;

            if (!MertToolbarHandoffMemory.IsAnyCustomToolOpen())
                return;

            if (!MertToolbarHandoffMemory.IsRoadsCategory(assetCategory))
                return;

            MertToolState.LiveUiCategory = assetCategory;
            MertToolState.LastResolvedCategory = assetCategory;
            MertToolState.UserJustChangedAssetCategory = true;
            MertToolState.BlockRoadPrefabFallbackUntilNextRealSelection = true;

            if (MertToolState.TabHandoffFromCategory != Entity.Null &&
                MertToolState.TabHandoffFromRoad != null &&
                MertToolState.TabHandoffFromCategory != assetCategory)
            {
                _ = MertToolbarHandoffMemory.TryInjectSourceCategoryMemory(
                    __instance,
                    MertToolState.TabHandoffFromCategory,
                    MertToolState.TabHandoffFromRoad);
            }

            MertToolState.ActivateTabHandoff(assetCategory);
            MertToolState.OnToolAbortedByUI?.Invoke(ToolExitMode.SilentTabClose);
        }
    }
    [HarmonyPatch(typeof(ToolbarUISystem), "Apply",
        new System.Type[]
        {
            typeof(List<Entity>),
            typeof(List<Entity>),
            typeof(Entity),
            typeof(Entity),
            typeof(Entity),
            typeof(bool)
        })]
    public static class ToolbarUISystem_Apply_HandoffPatch
    {
        public static void Prefix(
   
        ToolbarUISystem __instance,
            List<Entity> themes,
            List<Entity> packs,
            ref Entity assetMenuEntity,
            ref Entity assetCategoryEntity,
            ref Entity assetEntity,
            ref bool updateTool)
        {

            if (!MertToolState.TabHandoffActive && !MertToolState.PendingRestore)
                return;
            if (assetCategoryEntity == Entity.Null)
                return;
            if (assetCategoryEntity != MertToolState.TabHandoffToCategory)
                return;
            if (!MertToolbarHandoffMemory.IsRoadsCategory(assetCategoryEntity))
                return;

            bool incomingIsNull = assetEntity == Entity.Null;
            bool incomingIsStamp = false;

            if (!incomingIsNull &&
                MertToolbarHandoffMemory.TryResolvePrefab(assetEntity, out var incomingPrefab))
            {
                incomingIsStamp = MertToolbarHandoffMemory.IsOurStamp(incomingPrefab);
            }

            if (!incomingIsNull && !incomingIsStamp)
                return;

            if (MertToolState.TabHandoffFromRoad == null)
                return;

            if (!MertToolbarHandoffMemory.TryResolveEntity(MertToolState.TabHandoffFromRoad, out var realRoadEntity))
                return;

            assetEntity = realRoadEntity;
            updateTool = true;
        }
    }
    [HarmonyPatch(typeof(ToolbarUISystem), "SelectAsset", new System.Type[] { typeof(Entity), typeof(bool) })]
    public static class ToolbarUISystem_SelectAsset_HandoffPatch
    {
        public static void Prefix(ref Entity assetEntity, ref bool updateTool)
        {
            if (assetEntity == Entity.Null)
                return;

            if (MertToolState.SuppressUiMemoryCapture)
                return;

            if (MertToolState.SuppressLiveUiCapture)
                return;

            if (!MertToolbarHandoffMemory.IsAnyCustomToolOpen())
                return;

            if (!MertToolbarHandoffMemory.IsRoadNetPrefab(assetEntity, out var netPrefab))
                return;

            MertToolState.LiveUiRoadPrefab = netPrefab;
            MertToolState.LastResolvedRoadPrefab = netPrefab;
            MertToolState.BlockRoadPrefabFallbackUntilNextRealSelection = false;

            if (MertToolbarHandoffMemory.TryResolveCategoryFromAsset(assetEntity, out var category))
            {
                MertToolState.LiveUiCategory = category;
                MertToolState.LastResolvedCategory = category;
            }

            if (MertToolState.TabHandoffActive)
            {
                MertToolState.ClearTabHandoff();
            }

            if (!MertToolState.SuppressUiAbortDuringRestore)
            {
                MertToolState.OnToolAbortedByUI?.Invoke(ToolExitMode.UserSelectionClose);
            }
        }
    }
}
