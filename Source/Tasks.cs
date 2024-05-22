using HarmonyLib;

namespace AICore;

using System.Collections.Generic;
using System.Linq;

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