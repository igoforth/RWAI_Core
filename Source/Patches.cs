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

// Display AI Server status or welcome message at main menu
//
[StaticConstructorOnStartup]
[HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
public static class MainMenuDrawer_MainMenuOnGUI_Patch
{
    public static bool ShowWelcome => showWelcome;
    private static bool autoUpdate = AICoreMod.Settings != null && AICoreMod.Settings.AutoUpdateCheck;
    private static bool showWelcome;
    private static bool userOverride; // one-time decision per session if !isConfigured
    private static Texture2D Banner;
    private static bool attemptedDownscale;
    static MainMenuDrawer_MainMenuOnGUI_Patch()
    {
        LoadBanner(out Banner);
    }
    public static void Postfix()
    {
        Welcome();
        ShowStatus();
    }

    private static void LoadBanner(out Texture2D Banner)
    {
        Banner = new(1, 1);
        var bannerPath = Path.Combine(Directory.GetParent(AICoreMod.self!.Content.ModMetaData.PreviewImagePath).FullName, "WelcomeBanner.jpg");
        if (File.Exists(bannerPath))
        {
            var imageData = File.ReadAllBytes(bannerPath);
            _ = Banner.LoadImage(imageData);
        }
        else LogTool.Error("Banner image not found at: " + bannerPath);
    }

    public static int DownscaleBanner(int targetWidth)
    {
        var scaleDivisor = (double)(Banner.width / targetWidth);
        Banner = LanczosResize.DownsampleImage(Banner, scaleDivisor) ?? new(1, targetWidth);
        return Banner.height;
    }

    public static void Welcome()
    {
        if (userOverride) return;
        if (AICoreMod.Settings == null) return;
        if (AICoreMod.self == null) return;
        showWelcome = BootstrapTool.isConfigured == false;
        if (!showWelcome) return;

        var background = new Color(0f, 0f, 0f, 0.8f);
        var screen = new Vector2(UI.screenWidth, UI.screenHeight);
        var dialogSize = new Vector2(540, 540);
        var rect = new Rect((screen.x - dialogSize.x) / 2, (screen.y - dialogSize.y) / 2, dialogSize.x, dialogSize.y);
        var welcome = "RWAI_Welcome".Translate();

        Widgets.DrawBoxSolidWithOutline(rect, background, Color.white);

        // Initialize and draw the banner if not already done
        int rectWidth = Mathf.FloorToInt(rect.width); // Cast rect.width to int for comparison
        if (!attemptedDownscale)
        {
            _ = DownscaleBanner(rectWidth);
            attemptedDownscale = true;
        }
        float imageHeight = Banner.height * (rect.width / Banner.width);
        var imageRect = new Rect(rect.x, rect.y, rect.width, imageHeight);
        var uvRect = new Rect(0f, 0f, 1f, 1f);
        Widgets.DrawTexturePart(imageRect, uvRect, Banner);
        rect.y += imageHeight;
        rect.height -= imageHeight;

        var anchor = Text.Anchor;
        var font = Text.Font;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(rect.ExpandedBy(-20, -30), welcome);

        // Checkbox for automatic updates
        var checkboxRect = new Rect(rect.x + 20, rect.yMax - 80, rect.width - 40, 24);
        Widgets.CheckboxLabeled(checkboxRect, "RWAI_AutoUpdate".Translate(), ref autoUpdate);

        // Buttons for Continue and Cancel
        var buttonWidth = 80;
        var spacing = 10;
        var buttonRectContinue = new Rect(rect.x + rect.width - (2 * buttonWidth) - spacing - 20, rect.yMax - 40, buttonWidth, 30);
        var buttonRectCancel = new Rect(rect.x + rect.width - buttonWidth - 20, rect.yMax - 40, buttonWidth, 30);

        if (Widgets.ButtonText(buttonRectContinue, "Continue"))
        {
            AICoreMod.Settings.AutoUpdateCheck = autoUpdate; // Apply changes
            AICoreMod.Settings.Enabled = true;
            AICoreMod.Settings.Write(); // Save settings if needed

            // Dismiss dialog
            userOverride = true;
        }

        if (Widgets.ButtonText(buttonRectCancel, "Cancel"))
        {
            AICoreMod.Settings.AutoUpdateCheck = autoUpdate; // Apply changes
            AICoreMod.Settings.Enabled = false;
            AICoreMod.Settings.Write(); // Save settings if needed

            // Dismiss dialog
            userOverride = true;
        }

        Text.Anchor = anchor;
        Text.Font = font;
    }

    public static void ShowStatus()
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
#if DEBUG
        foreach (CodeInstruction instruction in codes.GetRange(i, 15))
            LogTool.Debug(instruction.ToString());
#endif

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
            { "MonitorJobs", new UpdateTaskTime(() => 5.0f, MonitorJobStatus.Task, false) },
            // { "RefreshExpansions", new UpdateTaskTime(() => 60.0f, RefreshExpansions.Task, false) },
            // { "BakeImages", new UpdateTaskTime(() => 5.0f, BakeImages.Task, false) }
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
