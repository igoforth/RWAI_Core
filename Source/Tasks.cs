namespace AICore;

// updates the ServerStatus
//
public static class UpdateServerStatus
{
    public static ServerManager.ServerStatus serverStatusEnum =
        ServerManager.currentServerStatusEnum;
    public static string serverStatus = ServerManager.currentServerStatus;

    public static void Task()
    {
        serverStatusEnum = ServerManager.currentServerStatusEnum;
        serverStatus = ServerManager.currentServerStatus;
    }
}

// monitor jobs for Server
//
public static class MonitorJobStatus
{
    public static void Task()
    {
        Tools.SafeAsync(AICoreMod.Client.MonitorJobsAsync);
    }
}

// update language for harmony patches
//
public static class UpdateLanguage
{
    public static SupportedLanguage activeLanguage = LanguageMapping.GetLanguage();
    public static void Task()
    {
        var newLanguage = LanguageMapping.GetLanguage();
        if (activeLanguage != newLanguage)
            activeLanguage = newLanguage;
    }
}

// update available model sizes
//
// public static class SettingsCache
// {
//     public static Dictionary<AICoreSettings.ModelSize, (string name, int size)>? AvailableModelSizes = AICoreMod.Settings?.AvailableModelSizes;
//     public static AICoreSettings.ModelSize? ActiveModelSize = AICoreMod.Settings?.ActiveModelSize;
//     public static void Task()
//     {
//         if (AICoreMod.Settings == null) return;
//         if (AICoreMod.Settings.AvailableModelSizes == null) return;
//         Push();
//         Pull();
//     }

//     private static void Push()
//     {
//         AvailableModelSizes = AICoreSettings.GetAvailableModelSizes();
//         if (AvailableModelSizes.Count != AICoreMod.Settings!.AvailableModelSizes!.Count)
//             AICoreMod.Settings.AvailableModelSizes = AvailableModelSizes;
//     }

//     private static void Pull()
//     {
//         if (AICoreMod.Settings!.ActiveModelSize != ActiveModelSize)
//             ActiveModelSize = AICoreMod.Settings!.ActiveModelSize;
//     }
// }