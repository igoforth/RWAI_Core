using System.Reflection;
using System.Reflection.Emit;
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

// Display AI Server status on main menu
//
[HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
public static class MainMenuDrawer_MainMenuOnGUI_Patch
{
    public static void Postfix()
    {
        Color background = new(0f, 0f, 0f, 0.8f);
        (int sw, int sh) = (UI.screenWidth, UI.screenHeight);
        float statusWidth = 150f;
        float statusHeight = 30f;
        float iconSize = 11f; // Size of the status icon
        float iconPadding = 10f; // Padding between icon and text

        Rect rect = new(sw - statusWidth - 10, sh - statusHeight - 10, statusWidth, statusHeight);

        // Draw semi-transparent background
        Widgets.DrawBoxSolid(rect, background);

        // Prepare to draw the text and icon
        TextAnchor anchor = Text.Anchor;
        GameFont font = Text.Font;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;

        // Calculate icon and text positions
        Rect iconRect =
            new(rect.x + iconPadding, rect.y + ((rect.height - iconSize) / 2), iconSize, iconSize);
        Rect textRect =
            new(
                iconRect.xMax + iconPadding,
                rect.y + 1, // lower just a tiny bit
                rect.width - iconRect.width - (3 * iconPadding),
                rect.height
            );

        // Draw the status texture based on the server status
        Texture2D statusTexture = Graphics.ButtonServerStatus[
            (int)UpdateServerStatus.serverStatusEnum
        ];
        if (statusTexture != null)
        {
            GUI.DrawTexture(iconRect, statusTexture);
        }

        // Draw the server status text
        Widgets.Label(textRect, UpdateServerStatus.serverStatus);

        // Restore text settings
        Text.Anchor = anchor;
        Text.Font = font;
    }
}

// public static void Postfix()
// {
//     if (showWelcome == false || AICoreMod.Settings.IsConfigured)
//     {
//         // UIRoot_Play_UIRootOnGUI_Patch.Postfix();
//         return;
//     }

//     var (sw, sh) = (UI.screenWidth, UI.screenHeight);
//     var (w, h) = (360, 120);
//     var rect = new Rect((sw - w) / 2, (sh - h) / 2, w, h);
//     var welcome =
//         "Welcome to RimGPT. You need to configure the mod before you can use it. Click here.";

//     Widgets.DrawBoxSolidWithOutline(rect, background, Color.white);
//     if (Mouse.IsOver(rect) && Input.GetMouseButton(0))
//     {
//         showWelcome = false;
//         Find.WindowStack.Add(new Dialog_ModSettings(AICoreMod.self));
//     }
//     var anchor = Text.Anchor;
//     var font = Text.Font;
//     Text.Font = GameFont.Small;
//     Text.Anchor = TextAnchor.MiddleCenter;
//     Widgets.Label(rect.ExpandedBy(-20, 0), welcome);
//     Text.Anchor = anchor;
//     Text.Font = font;
// }

// Transpile our fancy gui into the bottom right
//
[HarmonyPatch(typeof(GlobalControls), nameof(GlobalControls.GlobalControlsOnGUI))]
public static class GlobalControls_GlobalControlsOnGUI_Patch
{
    private static readonly MethodInfo m_GenUI_DrawTextWinterShadow = AccessTools.Method(
        typeof(GenUI),
        nameof(GenUI.DrawTextWinterShadow)
    );
    private static readonly MethodInfo m_GlobalControlsUtility_DoPlaySettings = AccessTools.Method(
        typeof(GlobalControlsUtility),
        nameof(GlobalControlsUtility.DoPlaySettings)
    );
    private static readonly MethodInfo m_PlayGUIServerStatusPatch = AccessTools.Method(
        typeof(GlobalControls_GlobalControlsOnGUI_Patch),
        nameof(PlayGUIServerStatusPatch)
    );

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new(instructions);

        // first find `GenUI.DrawTextWinterShadow`
        // then insert after `stloc.1`
        if (
            m_GenUI_DrawTextWinterShadow is null
            || m_PlayGUIServerStatusPatch is null
            || m_GlobalControlsUtility_DoPlaySettings is null
        )
        {
            goto transpiler_error;
        }

        int i = codes.FindIndex(instruction => instruction.Calls(m_GenUI_DrawTextWinterShadow));
        if (i == -1)
        {
            goto transpiler_error;
        }

        int j = codes.FindIndex(i, instruction => instruction.opcode == OpCodes.Stloc_1);
        if (j == -1)
        {
            goto transpiler_error;
        }

        if (j - i != 4)
        {
            goto transpiler_error;
        }

        int k = codes.FindIndex(instruction =>
            instruction.Calls(m_GlobalControlsUtility_DoPlaySettings)
        );
        if (k == -1)
        {
            goto transpiler_error;
        }

        // assert num2 -= 4;
        IEnumerable<CodeInstruction>? gadget_ScreenHeightDecrement =
            (
                new[]
                {
                    OpCodes.Ldloc,
                    OpCodes.Ldloc_0,
                    OpCodes.Ldloc_1,
                    OpCodes.Ldloc_2,
                    OpCodes.Ldloc_3
                }.Contains(codes[i + 1].opcode)
                && codes[i + 2].opcode == OpCodes.Ldc_R4
                && codes[i + 3].opcode == OpCodes.Sub
                && new[]
                {
                    OpCodes.Stloc,
                    OpCodes.Stloc_0,
                    OpCodes.Stloc_1,
                    OpCodes.Stloc_2,
                    OpCodes.Stloc_3
                }.Contains(codes[i + 4].opcode)
            )
                ?
                [
                    codes[i + 1].Clone(),
                    codes[i + 2].Clone(),
                    codes[i + 3].Clone(),
                    codes[i + 4].Clone()
                ]
                : null;
        if (gadget_ScreenHeightDecrement is null)
        {
            goto transpiler_error;
        }

        // assert num2 is last arg of DoPlaySettings
        IEnumerable<CodeInstruction>? gadget_ScreenHeightReference =
            (codes[k - 1].opcode == OpCodes.Ldloca_S) ? [codes[k - 1].Clone()] : null;
        if (gadget_ScreenHeightReference is null)
        {
            goto transpiler_error;
        }

        // ldloca.s  V_1	// load address of local 1 (as arg)
        // call      void AICore.GlobalControls_GlobalControlsOnGUI_Patch::PlayGUIServerStatusPatch(float32&)
        // ldloc.1		    // load local 1
        // ldc.r4    4	    // load immediate 4
        // sub		        // subtract
        // stloc.1		    // store local 1
        IEnumerable<CodeInstruction> newInstructions = gadget_ScreenHeightReference
            .Concat([new CodeInstruction(OpCodes.Call, m_PlayGUIServerStatusPatch)])
            .Concat(gadget_ScreenHeightDecrement);

        // Insert new instructions after the found index `j`
        codes.InsertRange(j + 1, newInstructions);

    // print new codes to log
    // foreach (CodeInstruction instruction in codes.GetRange(i, 15))
    //     LogTool.Message(instruction.ToString());

    transpiler_return:
        return codes.AsEnumerable();

    transpiler_error:
        LogTool.Error(
            $"Cannot find {m_GenUI_DrawTextWinterShadow} in GlobalControls.GlobalControlsOnGUI!"
        );
        goto transpiler_return;
    }

    private static void PlayGUIServerStatusPatch(ref float num2)
    {
        Color background = new(0f, 0f, 0f, 0.4f);
        float sw = UI.screenWidth;
        float statusWidth = 148f;
        float statusHeight = 30f;
        float iconSize = 11f; // Size of the status icon
        float iconPadding = 10f; // Padding between icon and text

        // patch num2 with our height
        num2 -= statusHeight;

        // position rect on screen
        Rect rect = new(sw - statusWidth - 2, num2, statusWidth, statusHeight);

        // Draw semi-transparent background
        Widgets.DrawBoxSolid(rect, background);

        // Prepare to draw the text and icon
        TextAnchor anchor = Text.Anchor;
        GameFont font = Text.Font;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;

        // Calculate icon and text positions
        Rect iconRect =
            new(rect.x + iconPadding, rect.y + ((rect.height - iconSize) / 2), iconSize, iconSize);
        Rect textRect =
            new(
                iconRect.xMax + iconPadding,
                rect.y + 1, // lower just a tiny bit
                rect.width - iconRect.width - (3 * iconPadding),
                rect.height
            );

        // Draw the status texture based on the server status
        Texture2D statusTexture = Graphics.ButtonServerStatus[
            (int)UpdateServerStatus.serverStatusEnum
        ];
        if (statusTexture != null)
        {
            GUI.DrawTexture(iconRect, statusTexture);
        }

        // Draw the server status text
        Widgets.Label(textRect, UpdateServerStatus.serverStatus);

        // Restore text settings
        Text.Anchor = anchor;
        Text.Font = font;
    }
}

// Add hook to GenerateImageDescription to inject our description or add to our work queue
//
[HarmonyPatch(typeof(CompArt), nameof(CompArt.GenerateImageDescription))]
public static class CompArt_GenerateImageDescription_Patch
{
    private static readonly object _workLock = new();

    public static void Postfix(CompArt __instance, ref TaggedString __result)
    {
        if (__instance == null) throw new ArgumentException("Instance in patch is null!");
        if (UpdateServerStatus.serverStatusEnum != ServerManager.ServerStatus.Online) return;

#if DEBUG
        LogTool.Debug("Handling CompArt type");
#endif

        // Determine if we have AI-Generated result for Thing already
        int hashCode = __instance.GetHashCode();
#if DEBUG
        LogTool.Debug($"HashCode: {hashCode}");
#endif

        UpdateItemDescriptions.ItemStatus itemStatus = UpdateItemDescriptions.GetStatus(hashCode);
#if DEBUG
        LogTool.Debug($"ItemStatus: {itemStatus}");
#endif

        switch (itemStatus)
        {
            case UpdateItemDescriptions.ItemStatus.Done:
                (string Title, string Description)? value = UpdateItemDescriptions.GetValues(
                    hashCode
                );
                if (value.HasValue)
                {
#if DEBUG
                    LogTool.Debug(
                        $"Value found: Title = {value.Value.Title}, Description = {value.Value.Description}"
                    );
#endif
                    __instance.titleInt = value.Value.Title;
                    __result = (TaggedString)value.Value.Description;
                    if (ITab_Art.cachedImageSource == __instance)
                    {
#if DEBUG
                        LogTool.Debug("Updating cached image description");
#endif
                        ITab_Art.cachedImageDescription = (TaggedString)value.Value.Description;
                    }
                }
                break;

            case UpdateItemDescriptions.ItemStatus.NotDone:
                lock (_workLock)
                {
#if DEBUG
                    LogTool.Debug("ItemStatus is NotDone. Submitting job.");
#endif
                    // Send thing as job with relevant info
                    // 1. send "Thing" from ScribeSaver.DebugOutputFor()
                    // 2. send Title, Description from CompArt.GenerateTitle(), CompArt.GenerateImageDescription()
                    string myDef = Scribe.saver.DebugOutputFor(__instance.parent);
#if DEBUG
                    LogTool.Debug($"myDef: {myDef}");
#endif
                    TaggedString description = __instance.taleRef.GenerateText(
                        TextGenerationPurpose.ArtDescription,
                        __instance.Props.descriptionMaker
                    );
#if DEBUG
                    LogTool.Debug($"Generated description: {description}");
#endif
                    string title = GenText.CapitalizeAsTitle(
                        __instance.taleRef.GenerateText(
                            TextGenerationPurpose.ArtName,
                            __instance.Props.nameMaker
                        )
                    );
#if DEBUG
                    LogTool.Debug($"Generated title: {title}");
#endif
                    UpdateItemDescriptions.SubmitJob(hashCode, myDef, title, description);
#if DEBUG
                    LogTool.Debug("Job submitted.");
#endif
                }
                break;

            case UpdateItemDescriptions.ItemStatus.Working:
            default:
#if DEBUG
                LogTool.Debug("ItemStatus is Working or default case.");
#endif
                break;
        }
    }
}

// // Token: 0x0600A8B8 RID: 43192 RVA: 0x003C014C File Offset: 0x003BE34C
// public static void DoPlaySettings(WidgetRow rowVisibility, bool worldView, ref float curBaseY)
// {
//     float num = curBaseY - TimeControls.TimeButSize.y;
//     rowVisibility.Init((float)UI.screenWidth, num, UIDirection.LeftThenUp, 141f, 4f);
//     Find.PlaySettings.DoPlaySettingsGlobalControls(rowVisibility, worldView);
//     curBaseY = rowVisibility.FinalY;
// }

// // add toggle button to play settings
// //
// [StaticConstructorOnStartup]
// [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
// public static class PlaySettings_DoPlaySettingsGlobalControls_Patch
// {
//     static readonly Texture2D icon = ContentFinder<Texture2D>.Get("ToggleRWAI");
//     public static void Postfix(WidgetRow row, bool worldView)
//     {
//         if (worldView)
//             return;
//         var previousState = AICoreMod.Settings.enabled;
//         row.ToggleableIcon(ref AICoreMod.Settings.enabled, icon, $"RWAI is {(AICoreMod.Settings.enabled ? "ON" : "OFF")}".Translate(), SoundDefOf.Mouseover_ButtonToggle);
//         if (previousState != AICoreMod.Settings.enabled)
//             AICoreMod.Settings.Write();
//     }
// }

// // Token: 0x0600A8B8 RID: 43192 RVA: 0x003C014C File Offset: 0x003BE34C
// public static void DoPlaySettings(WidgetRow rowVisibility, bool worldView, ref float curBaseY)
// {
//     float num = curBaseY - TimeControls.TimeButSize.y;
//     rowVisibility.Init((float)UI.screenWidth, num, UIDirection.LeftThenUp, 141f, 4f);
//     Find.PlaySettings.DoPlaySettingsGlobalControls(rowVisibility, worldView);
//     curBaseY = rowVisibility.FinalY;
// }

// anything that requires the map to be defined that we update on periodically
//
[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootUpdate))]
public static partial class GenerallTimeUpdates_Patch
{
    // Dictionary to keep our tasks
    private static readonly Dictionary<string, UpdateTaskTime> updateTasks =
        new()
        {
            { "ServerStatus", new UpdateTaskTime(() => 2.0f, UpdateServerStatus.Task, false) },
            { "MonitorJobs", new UpdateTaskTime(() => 5.0f, MonitorJobStatus.Task, false) }
        };

    public static void Postfix()
    {
        float currentTime = Time.realtimeSinceStartup;

        foreach (KeyValuePair<string, UpdateTaskTime> taskEntry in updateTasks.ToList())
        {
            string key = taskEntry.Key;
            UpdateTaskTime task = taskEntry.Value;

            if (currentTime >= task.nextUpdateTime)
            {
                task.nextUpdateTime = currentTime + task.updateIntervalFunc();
                task.action();
            }

            updateTasks[key] = task;
        }
    }
}
