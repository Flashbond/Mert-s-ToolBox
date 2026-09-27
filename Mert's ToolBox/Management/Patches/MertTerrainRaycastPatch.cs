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
        /// <summary>Lets the flatten preview replace overlay terrain hits with hits on the original terrain.</summary>
        public static void Postfix(ToolBaseSystem __instance, bool __result, ref RaycastHit hit)
        {
            if (!__result)
                return;

            MertToolBoxTerrainFlattenSystem.Instance?.TryCorrectRaycast(__instance, ref hit);
        }
    }
}
