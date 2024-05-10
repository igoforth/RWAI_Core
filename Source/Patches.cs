using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AICore;

// LogTool
//
[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.LongEventsOnGUI))]
public static class LongEventHandler_LongEventsOnGUI_Patch
{
    public static void Postfix()
    {
        LogTool.Log();
    }
}

// TODO: Hook with bootstrapper
//
[HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
public static class MainMenuDrawer_MainMenuOnGUI_Patch
{
    static bool showWelcome = true;
    static readonly Color background = new(0f, 0f, 0f, 0.8f);

    public static void Postfix()
    {
        if (showWelcome == false || AICoreMod.Settings.IsConfigured)
        {
            UIRoot_Play_UIRootOnGUI_Patch.Postfix();
            return;
        }

        var (sw, sh) = (UI.screenWidth, UI.screenHeight);
        var (w, h) = (360, 120);
        var rect = new Rect((sw - w) / 2, (sh - h) / 2, w, h);
        var welcome =
            "Welcome to RimGPT. You need to configure the mod before you can use it. Click here.";

        Widgets.DrawBoxSolidWithOutline(rect, background, Color.white);
        if (Mouse.IsOver(rect) && Input.GetMouseButton(0))
        {
            showWelcome = false;
            Find.WindowStack.Add(new Dialog_ModSettings(AICoreMod.self));
        }
        var anchor = Text.Anchor;
        var font = Text.Font;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect.ExpandedBy(-20, 0), welcome);
        Text.Anchor = anchor;
        Text.Font = font;
    }
}

// TODO: Track with job queue instead
//
[HarmonyPatch(typeof(DesignationManager), nameof(DesignationManager.AddDesignation))]
public static class DesignationManager_AddDesignation_Patch
{
    public static void Postfix(Designation newDes)
    {
        (string order, string targetLabel) = DesignationHelpers.GetOrderAndTargetLabel(newDes);

        // bail if its a plan, the AI gets confused and thinks we're building stuff when its just planning.  using string because
        // of mods.  it might not be full-proof but should cover most use-cases.
        if (targetLabel.ToLowerInvariant().Contains("plan"))
            return;

        DesignationQueueManager.EnqueueDesignation(OrderType.Designate, order, targetLabel);
    }
}

// Patch for Designator_Cancel to track when the player issues a cancel
[HarmonyPatch(typeof(Designator_Cancel))]
public static class Designator_Cancel_Patch
{
    [HarmonyPrefix, HarmonyPatch(nameof(Designator_Cancel.DesignateSingleCell))]
    public static void PrefixForDesignateSingleCell(IntVec3 c)
    {
        //Logger.Message($"track cancel cell at  {c}");
        DesignationHelpers.TrackCancelCell(c);
    }

    [HarmonyPrefix, HarmonyPatch(nameof(Designator_Cancel.DesignateThing))]
    public static void PrefixForDesignateThing(Thing t)
    {
        //Logger.Message($"track cancel thing  {t}");
        DesignationHelpers.TrackCancelThing(t);
    }
}

// Patch for DesignationManager.RemoveDesignation to process only player-initiated cancellations
[HarmonyPatch(typeof(DesignationManager), nameof(DesignationManager.RemoveDesignation))]
public static class DesignationManager_RemoveDesignation_Patch
{
    public static void Postfix(Designation des)
    {
        // Checks if the action was cancelled for either a Thing or Cell target, bailing out only if neither is found.
        bool wasCancelledByPlayer =
            des.target.Cell != null && DesignationHelpers.IsTrackedCancelCell(des.target.Cell);

        if (!wasCancelledByPlayer)
            return;

        (string order, string targetLabel) = DesignationHelpers.GetOrderAndTargetLabel(des);

        // Bail if it's a plan to avoid ChatGPT getting confused.
        if (targetLabel.ToLowerInvariant().Contains("plan"))
            return;

        DesignationQueueManager.EnqueueDesignation(OrderType.Cancel, order, targetLabel);
    }
}

// on every tick, regardless if game is paused or not
[HarmonyPatch(typeof(TickManager), "DoSingleTick")]
public static class TickManager_DoSingleTick_Patch
{
    public static void Postfix()
    {
        var map = Find.CurrentMap;
        if (map == null)
            return;
        DesignationQueueManager.Update();
    }
}
