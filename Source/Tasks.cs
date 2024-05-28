using HarmonyLib;

namespace AICore;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// updates the Colony Setting, including weather, date, name of colony, etc
// used to update the record keeper, which in turn is used by the AI as part of game state
//
public static class UpdateColonySetting
{
    public static void Task(Map map)
    {
        var currentWeather = map.weatherManager.curWeather;
        var currentSeason = GenLocalDate.Season(map);
        var seasonName = currentSeason.LabelCap();
        var tileIndex = map.Tile;
        var tileLatLong = Find.WorldGrid.LongLatOf(tileIndex);
        long currentTicks = Find.TickManager.TicksAbs;
        var fullDateString = GenDate.DateFullStringAt(currentTicks, tileLatLong);
        var totalDays = GenDate.DaysPassed;
        var years = totalDays / GenDate.DaysPerYear;
        var quadrums = (totalDays % GenDate.DaysPerYear) / GenDate.DaysPerQuadrum;
        var days = (totalDays % GenDate.DaysPerYear) % GenDate.DaysPerQuadrum;

        var settlementName = map.Parent.LabelCap;
        var biome = map.Biome;
        string biomeName = biome.LabelCap;
        var biomeDescription = biome.description;

        var quadrumsMonthsSeasons = new List<string>();
        for (var quadrumIndex = 0; quadrumIndex < 4; quadrumIndex++)
        {
            var quadrum = (Quadrum)quadrumIndex;
            var season = GenDate.Season(
                (quadrumIndex * GenDate.DaysPerQuadrum + 5) * GenDate.TicksPerDay,
                tileLatLong
            );
            quadrumsMonthsSeasons.Add($"{quadrum.Label()} is {season}");
        }
        var quadrumsMonthsSeasonsString = quadrumsMonthsSeasons.Join();

        var message =
            $"Current Season: {seasonName}, Yearly Seasons Overview: {quadrumsMonthsSeasonsString}\n "
            + $"Each Quadrum lasts 15 days, and there are 4 Quadrums per year\n"
            + $"Today is: {fullDateString}, The current Settlement name is: {settlementName}\n "
            + $"Our colony is {years} years {quadrums} quadrums {days} days old\n "
            + $"Current weather: {currentWeather.LabelCap}\n "
            + $"Temperature: {map.mapTemperature.OutdoorTemp:0.#}°C\n "
            + $"Area: {biomeName}, {biomeDescription}";
        // Personas.Add(message, 1);
        // RecordKeeper.ColonySetting = message;
        // Logger.Message($"RecordKeeper.ColonySetting: {RecordKeeper.ColonySetting}");
    }
}

// updates the ServerStatus
//
public static class UpdateServerStatus
{
    public static ServerManager.ServerStatus serverStatusEnum = ServerManager.serverStatusEnum;
    public static string serverStatus = ServerManager.serverStatus;

    public static void Task()
    {
        serverStatusEnum = ServerManager.serverStatusEnum;
        serverStatus = ServerManager.serverStatus;
    }
}

// updates the item descriptions
// search for items with quality indicator
// deep inspect to get xml
// submit each item as job
// make callback available that attempts to update item description
// stores a record of items that have been updated
//
public static class UpdateItemDescriptions
{
    public enum ItemStatus
    {
        Done,
        Working,
        NotDone
    }

    // Thing.GetHashCode(), workedOn (bool)
    private static ConcurrentDictionary<int, ItemStatus> objectStatuses =
        new ConcurrentDictionary<int, ItemStatus>();

    private static LazyCompressedDictionary objectValues = new LazyCompressedDictionary(
        Path.Combine(
            [
                Directory.GetParent(GenFilePaths.ConfigFolderPath).ToStringSafe(),
                "RWAI",
                "Items",
                "items.dat"
            ]
        )
    );

    public static ItemStatus GetStatus(int id)
    {
        if (!objectStatuses.ContainsKey(id))
            objectStatuses.TryAdd(id, ItemStatus.NotDone);
        else if (GetValues(id) != null)
            objectStatuses[id] = ItemStatus.Done;
        return objectStatuses[id];
    }

    public static (string, string)? GetValues(int id)
    {
        if (!objectStatuses.ContainsKey(id))
            return null;
        return objectValues.Get(id);
    }

    public static void SubmitJob(int id, string def, string title, string description) { }

    public static void Task()
    {
        var map = Find.CurrentMap;
        if (map == null)
            return;

        // Parallel.ForEach(
        //     objects,
        //     obj =>
        //     {
        //         LongTask(obj);
        //         objectStatuses[obj] = true;
        //     }
        // );

        // foreach (Thing thing3 in Find.CurrentMap.thingGrid.ThingsAt(intVec))
        // {
        //     if (!this.fullMode)
        //     {
        //         stringBuilder.AppendLine(thing3.LabelCap + " - " + thing3.ToString());
        //     }
        //     else
        //     {
        //         stringBuilder.AppendLine(Scribe.saver.DebugOutputFor(thing3));
        //         stringBuilder.AppendLine();
        //     }
        // }
    }

    public static async void Finish(int id, string Title, string Description)
    {
        await objectValues.AddOrUpdateAsync(id, Title, Description);
        if (objectStatuses.ContainsKey(id))
            objectStatuses[id] = ItemStatus.Done;
    }
}
