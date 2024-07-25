// #author habeebweeb
// #name Gizmo Interaction Fix
// #desc Only allows for one translation/scale gizmo to register mouse input at a time
using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;
using UnityEngine;

public static class GizmoInteractionFix
{
    private static Harmony harmony;
    private static bool gizmoInteracting;
    private static int interactingGizmoID = -1;

    public static void Main() =>
        harmony = Harmony.CreateAndPatchAll(typeof(GizmoInteractionFix));

    public static void Unload()
    {
        if (harmony is null)
            return;

        harmony.UnpatchSelf();
        harmony = null;
    }

    private static void UpdateInteracting(GizmoRender gizmoRender, GizmoRender.MOVETYPE startMoveType)
    {
        if (startMoveType == gizmoRender.beSelectedType)
            return;

        if (startMoveType is GizmoRender.MOVETYPE.NONE && NInput.GetMouseButton(0))
        {
            gizmoInteracting = true;
            interactingGizmoID = gizmoRender.GetInstanceID();
        }
        else if (gizmoRender.beSelectedType is GizmoRender.MOVETYPE.NONE)
        {
            gizmoInteracting = false;
            interactingGizmoID = -1;
        }
    }

    [HarmonyPatch(typeof(GizmoRender), nameof(GizmoRender.OnRenderObject))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> OnRenderObjectTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
    {
        var matcher = new CodeMatcher(instructions, ilGenerator)
            .MatchEndForward(
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(GizmoRender), nameof(GizmoRender.beSelectedType))),
                new CodeMatch(OpCodes.Stloc_0),
                new CodeMatch(OpCodes.Ldarg_0));

        var originalPosition = matcher.Pos;

        matcher
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(GizmoInteractionFix), nameof(gizmoInteracting))))
            .InsertBranchAndAdvance(OpCodes.Brfalse, originalPosition + 1)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Object), nameof(Object.GetInstanceID))),
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(GizmoInteractionFix), nameof(interactingGizmoID))));

        var branchPosition = matcher.Pos;

        var renderGizmoEndPosition = matcher
            .MatchStartForward(
                new CodeMatch(OpCodes.Ldloc_0),
                new CodeMatch(OpCodes.Brtrue),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(GizmoRender), nameof(GizmoRender.beSelectedType))))
            .Pos;

        matcher
            .Start()
            .Advance(branchPosition)
            .InsertBranchAndAdvance(OpCodes.Bne_Un, renderGizmoEndPosition)
            .MatchEndForward(
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(GizmoRender), nameof(GizmoRender.RenderGizmos))),
                new CodeMatch(OpCodes.Ret))
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GizmoInteractionFix), nameof(UpdateInteracting))));

        return matcher.InstructionEnumeration();
    }
}
