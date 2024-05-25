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
}
