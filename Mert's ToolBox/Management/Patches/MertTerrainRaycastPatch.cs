using System;
using Game.Tools;
using HarmonyLib;
using MertsToolBox.Systems;
using Unity.Entities;
using RaycastHit = Game.Common.RaycastHit;

namespace MertsToolBox.Management.Patches
{
    [HarmonyPatch(typeof(ToolBaseSystem), "GetRaycastResult",
        new[] { typeof(Entity), typeof(RaycastHit) },
        new[] { ArgumentType.Out, ArgumentType.Out })]
    public static class ToolBase_GetRaycastResult_FlattenPreviewPatch
    {
        private static bool s_Failed;

        /// <summary>Skips patching (instead of failing the whole mod) if a game update removed the target overload.</summary>
        public static bool Prepare() =>
            MertPatchGuard.TargetExists(nameof(ToolBase_GetRaycastResult_FlattenPreviewPatch), typeof(ToolBaseSystem), "GetRaycastResult",
                new[] { typeof(Entity).MakeByRefType(), typeof(RaycastHit).MakeByRefType() });

        /// <summary>Lets the flatten preview replace overlay terrain hits with hits on the original terrain.</summary>
        public static void Postfix(ToolBaseSystem __instance, bool __result, ref RaycastHit hit)
        {
            if (!__result || s_Failed
                || !MertToolState.FlattenToolActive
                || !MertToolState.FlattenGeometryEnabled
                || !MertToolState.FlattenBusy
                || !(__instance is ObjectToolSystem))
                return;

            try
            {
                MertToolBoxTerrainFlattenSystem.Instance?.TryCorrectRaycast(__instance, ref hit);
            }
            catch (Exception e)
            {
                s_Failed = true;
                MertPatchGuard.ReportFailure(nameof(ToolBase_GetRaycastResult_FlattenPreviewPatch), e);
            }
        }
    }
}
