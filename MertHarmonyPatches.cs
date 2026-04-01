using Game.Prefabs;
using Game.Tools;
using HarmonyLib;
using System;
using System.Reflection;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MertsToolBox
{
    [HarmonyPatch(typeof(Game.UI.InGame.ToolbarUISystem), "SelectAssetCategory")]
    public class ToolbarUISystem_SelectAssetCategory_Patch
    {
        public static void Prefix(Entity assetCategory)
        {
            if (MertToolState.SuppressCategoryCapture) return;
            if (MertToolState.SuppressUiAbortDuringRestore) return;

            MertToolState.UserJustChangedAssetCategory = true;
            MertToolState.BlockRoadPrefabFallbackUntilNextRealSelection = true;
            MertToolState.OnToolAbortedByUI?.Invoke(ToolExitMode.SilentTabClose);
            MertToolState.LastResolvedCategory = assetCategory;
        }
    }

    [HarmonyPatch(typeof(Game.UI.InGame.ToolbarUISystem), "SelectAsset", new Type[] { typeof(Entity), typeof(bool) })]
    public class ToolbarUISystem_SelectAsset_Patch
    {
        public static void Prefix(Entity assetEntity, bool updateTool)
        {
            string entityStr = assetEntity == Entity.Null ? "NULL" : assetEntity.Index.ToString();
           
            if (MertToolState.SuppressUiMemoryCapture) return;

            if (!MertToolState.SuppressUiAbortDuringRestore && updateTool && assetEntity != Entity.Null)
            {
                MertToolState.OnToolAbortedByUI?.Invoke(ToolExitMode.UserSelectionClose);
            }

            if (assetEntity == Entity.Null) return;

            var prefabSystem = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<PrefabSystem>();
            if (prefabSystem != null && prefabSystem.TryGetPrefab<PrefabBase>(assetEntity, out var prefab))
            {
                if (prefab is NetPrefab netPrefab)
                {
                    MertToolState.LastResolvedRoadPrefab = netPrefab;
                    MertToolState.BlockRoadPrefabFallbackUntilNextRealSelection = false;
                    MertToolState.UserJustChangedAssetCategory = false;
                }
            }
        }
    }
}