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
        if (gizmoInteracting && !NInput.GetMouseButton(0))
        {
            gizmoInteracting = false;
            interactingGizmoID = -1;
        }
        else if (startMoveType != gizmoRender.beSelectedType && startMoveType == GizmoRender.MOVETYPE.NONE)
        {
            gizmoInteracting = true;
            interactingGizmoID = gizmoRender.GetInstanceID();
        }
    }

    private static void VisibleUpdateInteracting(GizmoRender gizmoRender)
    {
        if (!gizmoInteracting || !NInput.GetMouseButton(0))
            return;

        if (gizmoRender.GetInstanceID() != interactingGizmoID)
            return;

        gizmoInteracting = false;
        interactingGizmoID = -1;
    }

    [HarmonyPatch(typeof(GizmoRender), nameof(GizmoRender.OnRenderObject))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> OnRenderObjectTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
    {
        var matcher = new CodeMatcher(instructions, ilGenerator);

        matcher

            // Find end of if (!Visible) block
            .MatchEndForward(
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldc_I4_0),
                new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(GizmoRender), nameof(GizmoRender.beSelectedType))))
            .Advance(1)

            // Disable gizmo interacting if the non-visible gizmo is the interacting gizmo
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GizmoInteractionFix), nameof(VisibleUpdateInteracting))))

            // Find end of initializing the mOVETYPE variable
            .MatchEndForward(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(GizmoRender), nameof(GizmoRender.beSelectedType))),
                new CodeInstruction(OpCodes.Stloc_0))
            .Advance(1)
            .CreateLabel(out var originalPosition)

            // Skip checking interacting gizmo and ID if no gizmo is being interacted with
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(GizmoInteractionFix), nameof(gizmoInteracting))),
                new CodeInstruction(OpCodes.Brfalse, originalPosition))

            // Add gizmo ID and interacting gizmo ID to the stack
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Object), nameof(Object.GetInstanceID))),
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(GizmoInteractionFix), nameof(interactingGizmoID))));

        var gizmoIDCheckPosition = matcher.Pos;

        matcher
            .End()

            // Find the "end" of the method and make a label to jump to
            .MatchStartBackwards(
                new CodeMatch(OpCodes.Ldloc_0),
                new CodeMatch(OpCodes.Brtrue),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(GizmoRender), nameof(GizmoRender.beSelectedType))))
            .CreateLabel(out var renderGizmoEndLabel)

            // Go back to id check position
            .Start()
            .Advance(gizmoIDCheckPosition)

            // Branch to end label if the gizmo id does not match the interacting gizmo ID
            .InsertAndAdvance(new CodeInstruction(OpCodes.Bne_Un, renderGizmoEndLabel))
            .End()

            // Update interaction status at the end
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GizmoInteractionFix), nameof(UpdateInteracting))));

        return matcher.InstructionEnumeration();
    }
}
