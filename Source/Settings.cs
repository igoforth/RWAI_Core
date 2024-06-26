using System.Collections.ObjectModel;
using RimWorld;
using UnityEngine;
using Verse;

namespace AICore;

public partial class AICoreSettings : ModSettings
{
    public bool Enabled
    {
        get => enabled;
        set
        {
            OnEnableSet(value);

            if (enabled != value)
            {
                enabled = value;

                // Only modify state (as a function of the buttons in Settings) if
                // isConfigured is true, meaning Welcome and Startup have had their opportunities
                // to greet the user, or set the default state
                // this will not trigger as a result of setting Enabled in Welcome
                // as isConfigured should be false
                if (BootstrapTool.IsConfigured)
                {
                    JobClient.UpdateRunningState(value);
                    ServerManager.UpdateRunningState(value);
                }
            }
        }
    }

    public bool AutoUpdateCheck
    {
        get => autoUpdateCheck;
        set => autoUpdateCheck = value;
    }

    public bool IsConfigured => Enabled && BootstrapTool.IsConfigured;

    public ModelSize ActiveModelSize
    {
        get => activeModelSize;
        set
        {
            activeModelSize = value;
            OnActiveModelSizeSet();
        }
    }
    public AICoreSettings()
    {
        UpdateAvailableModelSizes(ref AvailableModelSizes);
    }

    private void OnEnableSet(bool value)
    {
        if (MainMenuDrawer_MainMenuOnGUI_Patch.ShowWelcome) BootstrapTool.UpdateRunningState(value);
        else BootstrapTool.UpdateRunningState(AutoUpdateCheck);
    }

    private static void OnActiveModelSizeSet()
    {
        // calling the getter will check that the model exists
        // and update IsConfigured accordingly
        _ = BootstrapTool.IsConfigured;
    }

    private bool enabled;
    private bool autoUpdateCheck;
    private ModelSize activeModelSize;
    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref enabled, "enabled", false);
        Scribe_Values.Look(ref autoUpdateCheck, "autoUpdateCheck", false);
        Scribe_Values.Look(ref activeModelSize, "activeModelSize", ModelSize.MINI);
    }

    public enum ModelSize
    {
        MINI,
        SMALL,
        MEDIUM,
        CUSTOM
    }
    public static Dictionary<ModelSize, (string name, int size)> AvailableModelSizes = new()
    {
        { ModelSize.CUSTOM, ("Custom", 0) },
        { ModelSize.MINI, ("Mini", 2000) }
    };
    public static readonly Collection<ModelSize> ModelSizeOrder =
    [
        ModelSize.MEDIUM,
        ModelSize.SMALL,
        ModelSize.MINI,
        ModelSize.CUSTOM
    ];
    public static void UpdateAvailableModelSizes(ref Dictionary<ModelSize, (string name, int size)> availableModelSizes)
    {
        // china does not have access to huggingface.co, and only Mini is on modelscope.cn
        if (UpdateLanguage.activeLanguage is not SupportedLanguage.ChineseSimplified and not SupportedLanguage.ChineseTraditional)
        {
            if (BootstrapTool.VRAM > 4000 && !availableModelSizes.ContainsKey(ModelSize.SMALL)) availableModelSizes.Add(ModelSize.SMALL, ("Small", 4000));
            if (BootstrapTool.VRAM > 8000 && !availableModelSizes.ContainsKey(ModelSize.MEDIUM)) availableModelSizes.Add(ModelSize.MEDIUM, ("Medium", 8000));
        }
        else
        {
            _ = availableModelSizes.Remove(ModelSize.SMALL);
            _ = availableModelSizes.Remove(ModelSize.MEDIUM);
        }
    }

    private IEnumerable<Mod>? expansionMods;
    private const int cardSize = 90;
    private readonly Dictionary<string, Texture2D> previewImageCache = [];
    private readonly HashSet<string> attemptedPreviews = [];
    private static SupportedLanguage activeLanguage = UpdateLanguage.activeLanguage;
    public Vector2 scrollPosition = Vector2.zero;

    public void DoWindowContents(Rect inRect)
    {
        // Define the rects for both sections
        Rect settingsRect = new(inRect.x, inRect.y, inRect.width, inRect.height / 3);
        Rect modsListRect = new(inRect.x, settingsRect.yMax, inRect.width, inRect.height * 2 / 3);

        // Draw settings section
        DrawSettings(settingsRect);

        // Draw mods list section
        DrawModsList(modsListRect);

        // Show server status in bottom right
        // MainMenuDrawer_MainMenuOnGUI_Patch.ShowStatus();
    }

    private void DrawSettings(Rect rect)
    {
        expansionMods = (from mod in LoadedModManager.ModHandles
                         where mod.Content != null &&
                               mod.Content.ModMetaData != null &&
                               AICoreMod.expansionList.Contains(mod.Content.ModMetaData.Name) &&
                               mod.Content.ModMetaData.Active
                         select mod).ToList();

        if (activeLanguage != UpdateLanguage.activeLanguage)
        {
            activeLanguage = UpdateLanguage.activeLanguage;
            UpdateAvailableModelSizes(ref AvailableModelSizes);
        }


        Listing_Standard listing = new();
        listing.Begin(rect);

        // CHECKBOXES

        bool autoUpdate = AutoUpdateCheck;
        listing.CheckboxLabeled("RWAI_AutoUpdate".Translate(), ref autoUpdate);
        AutoUpdateCheck = autoUpdate;

        listing.Gap(4);

        // MODEL SIZE SELECTION

        if (listing.ButtonTextLabeledPct("RWAI_ModelSize".Translate(), AvailableModelSizes.TryGetValue(activeModelSize).name, 0.8f, TextAnchor.MiddleLeft, null, null, null))
        {
            List<FloatMenuOption> options = [];
            foreach (ModelSize size in ModelSizeOrder)
            {
                if (AvailableModelSizes.TryGetValue(size, out (string name, int vram) value))  // Check if the size is actually in the dictionary
                {
                    var (name, vram) = value;
                    options.Add(new FloatMenuOption(("RWAI_Model" + name).Translate(), delegate
                    {
                        ActiveModelSize = size;
                        Write();
                    }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                }
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        // BUTTONS

        listing.Gap(48f);

        Listing_Standard serverListing = new();
        var innerRect = new Rect(0f, 64f, rect.xMax, listing.CurHeight);
        var widthLeft = innerRect.width;
        serverListing.Begin(innerRect);

        var colWidth = widthLeft / 2;
        widthLeft -= colWidth;
        serverListing.ColumnWidth = colWidth;

        _ = serverListing.Label(UpdateServerStatus.serverStatus);

        serverListing.NewColumn();
        colWidth = (widthLeft / 4) - 17f;
        serverListing.ColumnWidth = colWidth;
        if (serverListing.ButtonText("Update"))
        {
            // BootstrapTool will automatically shut down client and server
            BootstrapTool.UpdateRunningState(true);
        }

        serverListing.NewColumn();
        serverListing.ColumnWidth = colWidth;
        if (serverListing.ButtonText("Start"))
        {
            Enabled = true;
            Write();
        }

        serverListing.NewColumn();
        serverListing.ColumnWidth = colWidth;
        if (serverListing.ButtonText("Stop"))
        {
            Enabled = false;
            Write();
        }

        serverListing.NewColumn();
        serverListing.ColumnWidth = colWidth;
        if (serverListing.ButtonText("Restart"))
        {
            Enabled = false;
            Enabled = true;
        }

        serverListing.End();

        listing.Gap(16f);

        _ = listing.Label("RWAI_Reset".Translate());
        GUI.color = Color.red;
        if (listing.ButtonText("RESET"))
        {
            // BootstrapTool will automatically shut down client and server
            BootstrapTool.Reset();
        }
        GUI.color = Color.white;

        listing.End();
    }

    private void DrawModsList(Rect rect)
    {
        // #if DEBUG
        //         LogTool.Debug("Entering DrawModsList method.");
        // #endif
        if (AICoreMod.self == null || expansionMods == null || !expansionMods.Any())
        {
#if DEBUG
            LogTool.Debug("AICoreMod.self is null or no expansion mods available.");
#endif

            Widgets.Label(rect, "No expansion mods detected.");
            return;
        }

        // #if DEBUG
        //         LogTool.Debug($"Found {expansionMods.Count()} expansion mods.");
        // #endif

        Widgets.DrawBoxSolid(rect, new Color(0.05f, 0.05f, 0.05f, 0.75f));
        Rect scrollContentRect = new(0f, 0f, rect.width - 16, expansionMods.Count() * (cardSize + 10));
        Widgets.BeginScrollView(rect, ref scrollPosition, scrollContentRect, true);

        float y = 0f;
        foreach (var mod in expansionMods)
        {
            Rect modRect = new(0f, y, scrollContentRect.width, cardSize);

            if (!previewImageCache.TryGetValue(mod.Content.ModMetaData.Name, out Texture2D? modPreviewCropped) && !attemptedPreviews.Contains(mod.Content.ModMetaData.Name))
            {
#if DEBUG
                LogTool.Debug($"Attempting to load preview for mod: {mod.Content.ModMetaData.Name}");
#endif

                var fullScalePreviewPath = Path.Combine(Directory.GetParent(mod.Content.ModMetaData.PreviewImagePath).FullName, "PreviewBanner.jpg");
                if (!File.Exists(fullScalePreviewPath))
                {
#if DEBUG
                    LogTool.Debug("Cannot find full resolution image for mod: " + mod.Content.ModMetaData.Name);
#endif
                    _ = attemptedPreviews.Add(mod.Content.ModMetaData.Name);
                    continue;
                }

                Texture2D modPreview = new(0, 0);
                modPreview.LoadImage(File.ReadAllBytes(fullScalePreviewPath));

                if (modPreview.isReadable)
                {
                    var scaleDivisor = (double)(modPreview.width / modRect.width);
                    if (scaleDivisor < 1)
                    {
#if DEBUG
                        LogTool.Debug("Scale divisor was less than 1 for mod: " + mod.Content.ModMetaData.Name);
#endif
                        _ = attemptedPreviews.Add(mod.Content.ModMetaData.Name);
                        continue;
                    }

                    var modPreviewDownsampled = LanczosResize.DownsampleImage(modPreview, scaleDivisor);
                    if (modPreviewDownsampled == null)
                    {
#if DEBUG
                        LogTool.Debug("Downsampling failed for mod: " + mod.Content.ModMetaData.Name);
#endif
                        _ = attemptedPreviews.Add(mod.Content.ModMetaData.Name);
                        continue;
                    }

                    var cropX = Mathf.FloorToInt(Mathf.Min(modPreviewDownsampled.width, modRect.width));
                    var cropY = Mathf.FloorToInt(Mathf.Min(modPreviewDownsampled.height, modRect.height));

#if DEBUG
                    LogTool.Debug($"Attempting to crop with dimensions: {cropX}x{cropY} for mod: {mod.Content.ModMetaData.Name}");
#endif
                    modPreviewCropped = LanczosResize.CropImage(modPreviewDownsampled, cropX, cropY, "left", offset: 0);
                    if (modPreviewCropped == null)
                    {
#if DEBUG
                        LogTool.Debug("Cropping failed for mod: " + mod.Content.ModMetaData.Name);
#endif
                        _ = attemptedPreviews.Add(mod.Content.ModMetaData.Name);
                        continue;
                    }

                    previewImageCache[mod.Content.ModMetaData.Name] = modPreviewCropped;
                }
                else
                {
#if DEBUG
                    LogTool.Debug($"No preview image found for mod: {mod.Content.ModMetaData.Name}");
#endif
                }

                _ = attemptedPreviews.Add(mod.Content.ModMetaData.Name);
            }

            if (modPreviewCropped != null && Widgets.ButtonImage(modRect, modPreviewCropped, tooltip: mod.Content.ModMetaData.Name))
            {
#if DEBUG
                LogTool.Debug($"Opening settings for mod: {mod.Content.ModMetaData.Name}");
#endif
                Find.WindowStack.Add(new Dialog_ModSettings(mod));
            }

            y += cardSize + 10;
        }

        Widgets.EndScrollView();

        // #if DEBUG
        //         LogTool.Debug("Exiting DrawModsList method.");
        // #endif
    }
}
