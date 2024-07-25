// #author habeebweeb
// #name Gizmo FOV Distortion Fix
// #desc Fixes gizmo size/orientation distortion when camera's fov changes
using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

public static class GizmoFOVDistortionFix
{
    private static Harmony harmony;

    public static void Main() =>
        harmony = Harmony.CreateAndPatchAll(typeof(GizmoFOVDistortionFix));

    public static void Unload()
    {
        if (harmony is null)
            return;

        harmony.UnpatchSelf();
        harmony = null;
    }

    [HarmonyPatch(typeof(GizmoRender), nameof(GizmoRender.RenderGizmos))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> RenderGizmosTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator) =>
        new CodeMatcher(instructions, ilGenerator)
            .MatchStartForward(
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldc_R4),
                new CodeMatch(OpCodes.Ldc_R4),
                new CodeMatch(OpCodes.Call))
            .Advance(1)
            .SetOperandAndAdvance(2f)
            .Advance(4)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldc_R4, UnityEngine.Mathf.Deg2Rad),
                new CodeInstruction(OpCodes.Mul))
            .Advance(4)
            .SetOperandAndAdvance(3.5f)
            .InstructionEnumeration();
}
