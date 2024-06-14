using UnityEngine;
using Verse;

namespace AICore;

[StaticConstructorOnStartup]
public static class Graphics
{
    // ServerStatus
    //     Online,
    //     Busy,
    //     Error,
    //     Offline
    public static readonly Texture2D[] ButtonServerStatus =
    [
        ContentFinder<Texture2D>.Get("ButtonOnline", true),
        ContentFinder<Texture2D>.Get("ButtonBusy", true),
        ContentFinder<Texture2D>.Get("ButtonError", true),
        ContentFinder<Texture2D>.Get("ButtonOffline", true)
    ];

    public static Texture2D? Banner;
    static Graphics()
    {
        LoadBanner();
    }

    private static void LoadBanner()
    {
        var bannerPath = Path.Combine(Directory.GetParent(AICoreMod.self!.Content.ModMetaData.PreviewImagePath).FullName, "WelcomeBanner.jpg");
        if (File.Exists(bannerPath))
        {
            Banner = new Texture2D(0, 0);
            var imageData = File.ReadAllBytes(bannerPath);
            _ = Banner.LoadImage(imageData);
        }
        else LogTool.Error("Banner image not found at: " + bannerPath);
    }

    public static int? DownscaleBanner(int targetWidth)
    {
        if (Banner == null) LoadBanner();
        if (Banner == null) return null;
        var scaleDivisor = (double)(Banner.width / targetWidth);
        Banner = LanczosResize.DownsampleImage(Banner, scaleDivisor);
        if (Banner == null)
        {
            LogTool.Error("Failed to downscale the banner image.");
            return null;
        }
        return Banner.height;
    }
}
