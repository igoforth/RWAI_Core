using System.Collections.Concurrent;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AICore;

// updates the Colony Setting, including weather, date, name of colony, etc
// used to update the record keeper, which in turn is used by the AI as part of game state
//
public static class UpdateColonySetting
{
    public static void Task(Map map)
    {
        if (map == null) throw new ArgumentException("Map is null!");

        WeatherDef currentWeather = map.weatherManager.curWeather;
        Season currentSeason = GenLocalDate.Season(map);
        string seasonName = currentSeason.LabelCap();
        int tileIndex = map.Tile;
        UnityEngine.Vector2 tileLatLong = Find.WorldGrid.LongLatOf(tileIndex);
        long currentTicks = Find.TickManager.TicksAbs;
        string fullDateString = GenDate.DateFullStringAt(currentTicks, tileLatLong);
        int totalDays = GenDate.DaysPassed;
        int years = totalDays / GenDate.DaysPerYear;
        int quadrums = totalDays % GenDate.DaysPerYear / GenDate.DaysPerQuadrum;
        int days = totalDays % GenDate.DaysPerYear % GenDate.DaysPerQuadrum;

        string settlementName = map.Parent.LabelCap;
        BiomeDef biome = map.Biome;
        string biomeName = biome.LabelCap;
        string biomeDescription = biome.description;

        List<string> quadrumsMonthsSeasons = [];
        for (int quadrumIndex = 0; quadrumIndex < 4; quadrumIndex++)
        {
            Quadrum quadrum = (Quadrum)quadrumIndex;
            Season season = GenDate.Season(
                ((quadrumIndex * GenDate.DaysPerQuadrum) + 5) * GenDate.TicksPerDay,
                tileLatLong
            );
            quadrumsMonthsSeasons.Add($"{quadrum.Label()} is {season}");
        }
        string quadrumsMonthsSeasonsString = quadrumsMonthsSeasons.Join();
        _ =
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
    private static readonly Lazy<ConcurrentDictionary<int, ItemStatus>> objectStatuses =
        new(() => new ConcurrentDictionary<int, ItemStatus>());

    private static readonly Lazy<
        CachedGZipStorage<(string Title, string Description)>
    > objectValues =
        new(
            () =>
                new CachedGZipStorage<(string Title, string Description)>(
                    Path.Combine(
                        Directory.GetParent(GenFilePaths.ConfigFolderPath).ToStringSafe(),
                        "RWAI Items",
                        "items.dat"
                    )
                )
        );

    private static ConcurrentDictionary<int, ItemStatus> ObjectStatuses => objectStatuses.Value;
    private static CachedGZipStorage<(string Title, string Description)> ObjectValues =>
        objectValues.Value;

    public static ItemStatus GetStatus(int HashCode)
    {
        if (!ObjectStatuses.ContainsKey(HashCode))
        {
            _ = ObjectStatuses.TryAdd(HashCode, ItemStatus.NotDone);
        }
        else if (GetValues(HashCode) != null)
        {
            ObjectStatuses[HashCode] = ItemStatus.Done;
        }

        return ObjectStatuses[HashCode];
    }

    public static (string Title, string Description)? GetValues(int HashCode)
    {
        return !ObjectStatuses.ContainsKey(HashCode) ? null : ObjectValues.Get(HashCode);
    }

    public static void SubmitJob(int HashCode, string XmlDef, string Title, string Description)
    {
        JobRequest.Types.ArtDescriptionJob artDescriptionJob =
            new()
            {
                HashCode = HashCode,
                XmlDef = XmlDef,
                Title = Title,
                Description = Description
            };
        JobRequest jobRequest = new() { ArtDescriptionJob = artDescriptionJob };
        AICoreMod.Client.AddJob(jobRequest, Finish);
        _ = ObjectStatuses.AddOrUpdate(
            HashCode,
            ItemStatus.Working,
            (key, oldValue) => ItemStatus.Working
        );
    }

    public static async Task Finish(JobResponse response)
    {
        if (response == null) throw new ArgumentException("Response in Finish callback is null!");

        switch (response.JobResultCase)
        {
            case JobResponse.JobResultOneofCase.ArtDescriptionResponse:
                JobResponse.Types.ArtDescriptionResponse result =
                    response.ArtDescriptionResponse
                    ?? throw new ArgumentException(
                        "ArtDescriptionResponse callback could not retrieve result!"
                    );
                if (result.Title.NullOrEmpty() || result.Description.NullOrEmpty())
                {
                    ObjectStatuses.AddOrUpdate(
                        result.HashCode,
                        ItemStatus.NotDone,
                        (key, oldValue) => ItemStatus.NotDone
                    );
                }
                else
                {
#if DEBUG
                    LogTool.Debug(
                        $"ArtDescriptionResponse callback got response:\n{result.Title}\n{result.Description}"
                    );
#endif
                    await ObjectValues
                        .AddOrUpdateAsync(result.HashCode, (result.Title, result.Description))
                        .ConfigureAwait(false);
                    ObjectStatuses.AddOrUpdate(
                        result.HashCode,
                        ItemStatus.Done,
                        (key, oldValue) => ItemStatus.Done
                    );
                }
                break;
            case JobResponse.JobResultOneofCase.None:
            default:
                // must throw error, we won't be able to determine HashCode from ObjectStatuses, which invalidates the dictionary
                throw new ArgumentException(
                    "ArtDescriptionResponse callback could not parse response!"
                );
        }
    }
}
