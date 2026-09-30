using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Game.Simulation;
using HarmonyLib;
using MertsToolBox.Core;

namespace MertsToolBox.Management.Patches
{
    /// <summary>
    /// Community fix for a vanilla native heap overflow in TerrainSystem that crashes the game (CTD) in cities with
    /// many intersections, e.g. several large road grids.
    ///
    /// TerrainSystem.CullForCascades sizes the terrain lane list from the road entity count:
    ///     int num = m_RoadsGroup.CalculateEntityCountWithoutFiltering() * 6;
    ///     if (num > m_LaneCullList.Capacity) m_LaneCullList.Capacity = num + max(num / 4, 250);
    /// CullRoadsJob (Burst) then appends with ParallelWriter.AddNoResize, which has no capacity check in the player
    /// build, so extra sections are written past the buffer and corrupt unrelated heap memory; the game dies later
    /// somewhere else (mostly rendering).
    ///
    /// Sections per road edge, from CullRoadsJob: AddEdge = 2 segments; AddNode per edge end = 2 segments at a simple
    /// node (straight / bend), 4 at a junction or when a middle radius exists. Each AddSegment writes 1 section, plus
    /// up to 2 more only on tunnel / lowered transition edges. So:
    ///     straight road 2 + 2 + 2 = 6 (the vanilla assumption), any junction-to-junction edge 2 + 4 + 4 = 10.
    /// Measured in game: every 10x10 grid = 220 edges, 2184 sections (9.93 per edge), exactly as computed; vanilla
    /// overflowed on 24 of 41 grid placements, up to 3.5 MB past the buffer. With factor 12: no overflow, no crash.
    ///
    /// The formula guarantees a capacity of at least factor * roads, so the factor must exceed the real sections per
    /// road. 12 covers the 10 of a fully junctioned network with headroom for the rare transition edges.
    ///
    /// This transpiler changes only that constant and nothing else in the method:
    ///   - factor below k_MinSafe (vanilla 6)          -> raised to k_Factor
    ///   - factor already k_MinSafe or higher          -> left alone (another mod with this fix, e.g. the standalone
    ///                                                    version / Mert's ToolBox, or a game update fixed it)
    ///   - pattern not found exactly once              -> left alone (game code changed)
    /// Several copies of this patch can run in any order; the first raises the factor, the others see it is done.
    /// </summary>
    [HarmonyPatch(typeof(TerrainSystem), "CullForCascades")]
    internal static class MertLaneCullCapacityPatch
    {
        private const int k_Factor = 12;
        private const int k_MinSafe = 10;

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            int matches = 0;
            int index = -1;
            int factor = 0;

            // Pattern: call CalculateEntityCountWithoutFiltering ; <int constant> ; mul
            for (int i = 0; i + 2 < list.Count; i++)
            {
                if ((list[i].opcode == OpCodes.Call || list[i].opcode == OpCodes.Callvirt)
                    && list[i].operand is MethodInfo m && m.Name == "CalculateEntityCountWithoutFiltering"
                    && TryGetInt(list[i + 1], out int value)
                    && list[i + 2].opcode == OpCodes.Mul)
                {
                    matches++;
                    index = i + 1;
                    factor = value;
                }
            }

            if (matches != 1)
            {
                ModRuntime.Log($"[LANECULL] TerrainSystem.CullForCascades not patched (pattern found {matches}x) - game code changed?");
                return list;
            }

            if (factor >= k_MinSafe)
            {
                ModRuntime.Log($"[LANECULL] terrain lane list factor is already {factor} (fixed by another mod or the game) - nothing to do");
                return list;
            }

            list[index] = new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)k_Factor).MoveLabelsFrom(list[index]);
            ModRuntime.Log($"[LANECULL] terrain lane list capacity fix active (factor {factor} -> {k_Factor})");
            return list;
        }

        /// <summary>Reads the value of any int32 constant load instruction.</summary>
        private static bool TryGetInt(CodeInstruction ins, out int value)
        {
            OpCode op = ins.opcode;
            if (op == OpCodes.Ldc_I4_M1) { value = -1; return true; }
            if (op == OpCodes.Ldc_I4_0) { value = 0; return true; }
            if (op == OpCodes.Ldc_I4_1) { value = 1; return true; }
            if (op == OpCodes.Ldc_I4_2) { value = 2; return true; }
            if (op == OpCodes.Ldc_I4_3) { value = 3; return true; }
            if (op == OpCodes.Ldc_I4_4) { value = 4; return true; }
            if (op == OpCodes.Ldc_I4_5) { value = 5; return true; }
            if (op == OpCodes.Ldc_I4_6) { value = 6; return true; }
            if (op == OpCodes.Ldc_I4_7) { value = 7; return true; }
            if (op == OpCodes.Ldc_I4_8) { value = 8; return true; }
            if (op == OpCodes.Ldc_I4_S && ins.operand != null) { value = System.Convert.ToInt32(ins.operand); return true; }
            if (op == OpCodes.Ldc_I4 && ins.operand != null) { value = System.Convert.ToInt32(ins.operand); return true; }
            value = 0;
            return false;
        }
    }
}
