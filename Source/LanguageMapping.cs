using Steamworks;
using UnityEngine;
using Verse;
using Verse.Steam;

namespace AICore;

// private static readonly List<string> SupportedAutoSelectLanguages = new List<string> {
// "Arabic", "ChineseSimplified", "ChineseTraditional", "Czech", "Danish", "Dutch", "English", "Estonian", "Finnish", "French", "German", "Hungarian", "Italian", "Japanese", "Korean", "Norwegian", "Polish", "Portuguese", "PortugueseBrazilian", "Romanian", "Russian", "Slovak", "Spanish", "SpanishLatin", "Swedish", "Turkish", "Ukrainian"
// };

// enum SupportedLanguage {
//   ARABIC = 0;
//   CHINESE_SIMPLIFIED = 1;
//   CHINESE_TRADITIONAL = 2;
//   CZECH = 3;
//   DANISH = 4;
//   DUTCH = 5;
//   ENGLISH = 6;
//   ESTONIAN = 7;
//   FINNISH = 8;
//   FRENCH = 9;
//   GERMAN = 10;
//   HUNGARIAN = 11;
//   ITALIAN = 12;
//   JAPANESE = 13;
//   KOREAN = 14;
//   NORWEGIAN = 15;
//   POLISH = 16;
//   PORTUGUESE = 17;
//   PORTUGUESE_BRAZILIAN = 18;
//   ROMANIAN = 19;
//   RUSSIAN = 20;
//   SLOVAK = 21;
//   SPANISH = 22;
//   SPANISH_LATIN = 23;
//   SWEDISH = 24;
//   TURKISH = 25;
//   UKRAINIAN = 26;
// }

public static class LanguageMapping
{
    public static readonly Dictionary<string, SupportedLanguage> LanguageMap =
        new()
        {
            { "Arabic", SupportedLanguage.Arabic },
            { "ChineseSimplified", SupportedLanguage.ChineseSimplified },
            { "ChineseTraditional", SupportedLanguage.ChineseTraditional },
            { "Czech", SupportedLanguage.Czech },
            { "Danish", SupportedLanguage.Danish },
            { "Dutch", SupportedLanguage.Dutch },
            { "English", SupportedLanguage.English },
            { "Estonian", SupportedLanguage.Estonian },
            { "Finnish", SupportedLanguage.Finnish },
            { "French", SupportedLanguage.French },
            { "German", SupportedLanguage.German },
            { "Hungarian", SupportedLanguage.Hungarian },
            { "Italian", SupportedLanguage.Italian },
            { "Japanese", SupportedLanguage.Japanese },
            { "Korean", SupportedLanguage.Korean },
            { "Norwegian", SupportedLanguage.Norwegian },
            { "Polish", SupportedLanguage.Polish },
            { "Portuguese", SupportedLanguage.Portuguese },
            { "PortugueseBrazilian", SupportedLanguage.PortugueseBrazilian },
            { "Romanian", SupportedLanguage.Romanian },
            { "Russian", SupportedLanguage.Russian },
            { "Slovak", SupportedLanguage.Slovak },
            { "Spanish", SupportedLanguage.Spanish },
            { "SpanishLatin", SupportedLanguage.SpanishLatin },
            { "Swedish", SupportedLanguage.Swedish },
            { "Turkish", SupportedLanguage.Turkish },
            { "Ukrainian", SupportedLanguage.Ukrainian }
        };

    public static SupportedLanguage GetLanguage()
    {
#if DEBUG
        LogTool.Debug("Attempting to get language using full folderName");
        string fullFolderName = LanguageDatabase.activeLanguage.folderName;
        if (LanguageMap.TryGetValue(fullFolderName.Trim(), out SupportedLanguage mappedLangFull))
        {
            LogTool.Debug($"Found language in LanguageMap using full folderName: {mappedLangFull}");
            return mappedLangFull;
        }

        LogTool.Debug("Attempting to get language using trimmed folderName");
        string trimmedFolderName = fullFolderName.Split(' ')[0];
        if (LanguageMap.TryGetValue(trimmedFolderName.Trim(), out SupportedLanguage mappedLangTrimmed))
        {
            LogTool.Debug($"Found language in LanguageMap using trimmed folderName: {mappedLangTrimmed}");
            return mappedLangTrimmed;
        }

        // If the above fails, check Steam's current game language if Steam is initialized
        LogTool.Debug("Checking Steam's current game language");
        if (SteamManager.Initialized)
        {
            var steamLang = SteamApps.GetCurrentGameLanguage().CapitalizeFirst();
            LogTool.Debug($"Steam's current game language after capitalization: {steamLang}");
            if (steamLang is not null && LanguageMap.TryGetValue(steamLang.Trim(), out SupportedLanguage mappedLang2))
            {
                LogTool.Debug($"Found language in LanguageMap for steamLang: {mappedLang2}");
                return mappedLang2;
            }
        }

        // If both the above checks fail, use the system's language
        LogTool.Debug("Using system's language setting");
        var appLang = Application.systemLanguage.ToStringSafe();
        LogTool.Debug($"System language after ToStringSafe: {appLang}");
        if (appLang is not null && LanguageMap.TryGetValue(appLang.Trim(), out SupportedLanguage mappedLang3))
        {
            LogTool.Debug($"Found language in LanguageMap for appLang: {mappedLang3}");
            return mappedLang3;
        }

        // Default to English if all else fails
        LogTool.Debug("Defaulting to English");
        return SupportedLanguage.English;
#else
        string folderName = LanguageDatabase.activeLanguage.folderName;
        string steamLang = SteamManager.Initialized ? SteamApps.GetCurrentGameLanguage().CapitalizeFirst() : "";
        string appLang = Application.systemLanguage.ToStringSafe();

        return (folderName, steamLang, appLang) switch
        {
            var full when LanguageMap.TryGetValue(full.folderName.Trim(), out var fullMapped) => fullMapped,
            var trimmed when trimmed.folderName.Contains(" ") && LanguageMap.TryGetValue(trimmed.folderName.Split(' ')[0].Trim(), out var trimmedMapped) => trimmedMapped,
            var steam when LanguageMap.TryGetValue(steam.steamLang.Trim(), out var steamMapped) => steamMapped,
            var app when LanguageMap.TryGetValue(app.appLang.Trim(), out var appMapped) => appMapped,
            _ => SupportedLanguage.English
        };
#endif
    }
}
