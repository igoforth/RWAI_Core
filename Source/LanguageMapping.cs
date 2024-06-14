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
        return
            LanguageDatabase.activeLanguage?.folderName is string folderLang
            && LanguageMap.TryGetValue(
                folderLang,
                out SupportedLanguage mappedLang1
            )
            ? mappedLang1
            : SteamManager.Initialized
            && SteamApps.GetCurrentGameLanguage().CapitalizeFirst() is string steamLang
            && LanguageMap.TryGetValue(steamLang, out SupportedLanguage mappedLang2)
                ? mappedLang2
                : Application.systemLanguage.ToStringSafe() is string appLang
                && LanguageMap.TryGetValue(
                    appLang,
                    out SupportedLanguage mappedLang3
                )
                    ? mappedLang3
                    : SupportedLanguage.English;
    }
}
