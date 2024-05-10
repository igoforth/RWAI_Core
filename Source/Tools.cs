using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RimWorld;
using Steamworks;
using Verse;
using Verse.AI;
using Verse.Steam;

namespace AICore;

public static class Tools
{
    public static bool DEBUG = false;
    public static readonly Regex tagRemover =
        new("<color.+?>(.+?)</(?:color)?>", RegexOptions.Singleline);

    public readonly struct Strings
    {
        public static readonly string colonist = "Colonist".TranslateSimple();
        public static readonly string enemy = "Enemy".TranslateSimple().CapitalizeFirst();
        public static readonly string visitor = "LetterLabelSingleVisitorArrives".TranslateSimple();
        public static readonly string mechanoid = "Mechanoid";

        public static readonly string information = "ThingInfo".TranslateSimple();
        public static readonly string completed = "StudyCompleted"
            .TranslateSimple()
            .CapitalizeFirst();
        public static readonly string finished = "Finished".TranslateSimple();
        public static readonly string dismiss = "CommandShuttleDismiss".TranslateSimple();
        public static readonly string priority = "Priority".TranslateSimple();
    }

    public static string PlayerName()
    {
        if (SteamManager.Initialized == false)
            return null;
        var name = SteamFriends.GetPersonaName();
        return name;
    }

    public static bool NonEmpty(this string str) => string.IsNullOrEmpty(str) == false;

    public static bool NonEmpty(this TaggedString str) => string.IsNullOrEmpty(str) == false;

    public static string OrderString(this Designation des)
    {
        return des.def.label.NonEmpty()
            ? des.def.label
            : des.def.LabelCap.NonEmpty()
                ? des.def.LabelCap
                : des.def.description.NonEmpty()
                    ? des.def.description
                    : des.def.defName;
    }

    public static string FormatNumber(long num) =>
        num switch
        {
            >= 1000000000 => (num / 1000000000D).ToString("0.#") + "B",
            >= 1000000 => (num / 1000000D).ToString("0.#") + "M",
            >= 100000 => (num / 1000D).ToString("0") + "K",
            >= 1000 => (num / 1000D).ToString("0.#") + "K",
            _ => num.ToString()
        };

    public static void SafeAsync(Func<Task> function)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await function();
            }
            catch (Exception ex)
            {
                LogTool.Error(ex.ToString());
            }
        });
    }

    public static async Task SafeWait(int milliseconds)
    {
        if (milliseconds == 0)
            return;
        try
        {
            await Task.Delay(milliseconds, AICoreMod.onQuit.Token);
        }
        catch (TaskCanceledException) { }
    }

    public static void SafeLoop(Action action, int loopDelay = 0)
    {
        _ = Task.Run(async () =>
        {
            while (AICoreMod.Running)
            {
                await SafeWait(loopDelay);
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    LogTool.Error(ex.ToString());
                    await SafeWait(1000);
                }
            }
        });
    }

    public static void SafeLoop(Func<Task> function, int loopDelay = 0)
    {
        _ = Task.Run(async () =>
        {
            while (AICoreMod.Running)
            {
                await SafeWait(loopDelay);
                try
                {
                    await function();
                }
                catch (Exception ex)
                {
                    LogTool.Error(ex.ToString());
                    await SafeWait(1000);
                }
            }
        });
    }

    public static void SafeLoop(Func<Task<bool>> function, int loopDelay = 0)
    {
        _ = Task.Run(async () =>
        {
            while (AICoreMod.Running)
            {
                await SafeWait(loopDelay);
                try
                {
                    if (await function())
                        continue;
                }
                catch (Exception ex)
                {
                    LogTool.Error(ex.ToString());
                    await SafeWait(1000);
                }
            }
        });
    }

    public static string Type(this Pawn pawn)
    {
        if (pawn.HostileTo(Faction.OfPlayer))
        {
            if (pawn.RaceProps.IsMechanoid)
                return Strings.mechanoid;
            else
                return Strings.enemy;
        }
        if (pawn.IsColonist || pawn.IsColonyMech)
            return Strings.colonist;
        return Strings.visitor;
    }

    public static string NameAndType(this Pawn pawn)
    {
        return $"{pawn.Type()} '{pawn.LabelShortCap}'";
    }

    public static string ToGameStringFromPOVWithType(this LogEntry entry, Pawn pawn)
    {
        if (pawn == null)
            return null;
        if (pawn.IsColonist == false)
            return null;
        var result = entry.ToGameStringFromPOV(pawn, false);
        var pawns = pawn.Map.mapPawns.AllPawnsSpawned.ToArray();
        for (var i = 0; i < pawns.Length; i++)
        {
            var p = pawns[i];
            if (p.RaceProps.Humanlike)
                result = result.Replace(p.LabelShortCap, p.NameAndType());
        }
        return result;
    }

    public static void ExtractPawnsFromLog(LogEntry entry, out Pawn from, out Pawn to)
    {
        from = null;
        to = null;

        if (entry is BattleLogEntry_Event @event)
        {
            from = @event.initiatorPawn;
            to = @event.subjectPawn;
        }
        else if (entry is BattleLogEntry_DamageTaken damage)
        {
            from = damage.initiatorPawn;
            to = damage.recipientPawn;
        }
        else if (entry is BattleLogEntry_ExplosionImpact explosion)
        {
            from = explosion.initiatorPawn;
            to = explosion.recipientPawn;
        }
        else if (entry is BattleLogEntry_MeleeCombat melee)
        {
            from = melee.initiator;
            to = melee.recipientPawn;
        }
        else if (entry is BattleLogEntry_RangedFire fire)
        {
            from = fire.initiatorPawn;
            to = fire.recipientPawn;
        }
        else if (entry is BattleLogEntry_RangedImpact impact)
        {
            from = impact.initiatorPawn;
            to = impact.recipientPawn;
        }
        else if (entry is BattleLogEntry_StateTransition transition)
            from = transition.subjectPawn;
    }

    // gets the job label from a specific pawn
    public static string GetJobLabelFromPawn(Job job, Pawn driverPawn)
    {
        if (job == null)
        {
            // Returns "Working" as a safe default if job is null.
            return "Working";
        }

        try
        {
            var report = job.GetReport(driverPawn);
            // Ensures report is not null or empty before trying to capitalize.
            if (!string.IsNullOrEmpty(report))
            {
                return report.CapitalizeFirst();
            }
        }
        catch
        {
            // In case of an exception, return "Working" as a safe default.
            return "Working";
        }

        // If GetReport returned null or an empty string, return "Working".
        return "Working";
    }

    // simple pluralization tool, not exhaustive and doesnt cover all cases.
    public static string SimplePluralize(string noun)
    {
        // Basic pluralization rule: add 's' or 'es'
        // Note: This does not cover all English language special cases.
        if (
            noun.EndsWith("s")
            || noun.EndsWith("sh")
            || noun.EndsWith("ch")
            || noun.EndsWith("x")
            || noun.EndsWith("z")
        )
        {
            return $"{noun}es";
        }
        else if (noun.EndsWith("y") && noun.Length > 1 && !"aeiou".Contains(noun[noun.Length - 2]))
        {
            // Words ending in 'y' following a consonant should change the 'y' to 'ies'
            return $"{noun.Substring(0, noun.Length - 1)}ies";
        }
        else if (noun.EndsWith("f") || noun.EndsWith("fe"))
        {
            // Words ending in 'f' or 'fe' may change to "ves" in the plural form
            if (noun.EndsWith("fe"))
            {
                return $"{noun.Substring(0, noun.Length - 2)}ves";
            }
            else
            {
                return $"{noun.Substring(0, noun.Length - 1)}ves";
            }
        }
        // Default pluralization
        else
        {
            return $"{noun}s";
        }
    }

    public static string GetIndefiniteArticleFor(string noun)
    {
        // Check for null, empty string, or white space string.
        if (string.IsNullOrWhiteSpace(noun))
        {
            return string.Empty; // Return an empty string if there's an issue.
        }

        try
        {
            char firstLetter = noun.TrimStart()[0];
            bool isVowel = "aeiouAEIOU".IndexOf(firstLetter) >= 0;
            return isVowel ? "an" : "a";
        }
        catch
        {
            // If an exception occurs (e.g., the string is empty after TrimStart), return an empty string.
            return string.Empty;
        }
    }

    public static readonly string[] commonLanguages =
    [
        "Alien",
        "Arabic",
        "Bengali",
        "Bulgarian",
        "Catalan",
        "Chinese",
        "Croatian",
        "Czech",
        "Danish",
        "Dutch",
        "English",
        "Estonian",
        "Finnish",
        "French",
        "German",
        "Greek",
        "Hebrew",
        "Hindi",
        "Hungarian",
        "Icelandic",
        "Indonesian",
        "Italian",
        "Japanese",
        "Korean",
        "Latvian",
        "Lithuanian",
        "Malay",
        "Norwegian",
        "Persian",
        "Polish",
        "Portuguese",
        "Punjabi",
        "Romanian",
        "Russian",
        "Serbian",
        "Slovak",
        "Slovenian",
        "Spanish",
        "Swedish",
        "Tamil",
        "Telugu",
        "Thai",
        "Turkish",
        "Ukrainian",
        "Urdu",
        "Vietnamese",
        "Welsh",
        "Yiddish"
    ];
}
