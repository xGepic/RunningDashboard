using System.Net.Http.Json;

namespace RunningDashboard.Api.Tests.Harness;

// The JSON of /api/runs and /api/runs/{id}, as the tests read it.

public sealed record RunPage(List<RunResponse> Items, int Page, int PageSize, int TotalCount)
{
    public List<string> Names => [.. Items.Select(i => i.Name)];
}

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="Pace">Seconds per km.</param>
/// <param name="ShoeName">None for a Run without a Shoe.</param>
public sealed record RunResponse(long Id, string Name, string SportType, DateTime StartLocal, double Distance, int MovingTime, double? Pace, string? ShoeName);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="ElapsedTime">Seconds.</param>
/// <param name="Pace">Seconds per km.</param>
/// <param name="Detail">"pending", "loaded" or "unavailable": whether Strava's detailed Activity is stored.</param>
/// <param name="Route">The route as an encoded polyline; none for a Run without GPS.</param>
public sealed record RunDetailResponse(
    long Id,
    string Name,
    string SportType,
    DateTime StartLocal,
    double Distance,
    int MovingTime,
    int ElapsedTime,
    double? Pace,
    double ElevationGain,
    double? RelativeEffort,
    double? AverageHeartRate,
    string StravaUrl,
    string? ShoeName,
    string Detail,
    string? Route,
    List<BestEffortResponse> BestEfforts,
    List<SplitResponse> Splits,
    List<SplitResponse> MileSplits,
    RunFitnessResponse? Fitness);

/// <param name="Distance">Metres.</param>
/// <param name="ElapsedTime">Seconds.</param>
/// <param name="TimePerKm">Seconds.</param>
public sealed record BestEffortResponse(string Name, double Distance, int ElapsedTime, double? TimePerKm, bool PersonalBest);

/// <param name="Index">1-based.</param>
/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="Pace">Seconds per km, or per mile for a mile split.</param>
public sealed record SplitResponse(int Index, double Distance, int MovingTime, double? Pace, double ElevationDifference);

public sealed record RunFitnessResponse(double? AerobicEfficiency, double? Cadence, bool IsLongRun, double? CardiacDrift);

public static class RunApi
{
    /// <param name="query">E.g. "shoe=g31444377&amp;sort=distance", without the "?".</param>
    public static async Task<RunPage> ListRunsAsync(this HttpClient browser, string query = "") =>
        (await browser.GetFromJsonAsync<RunPage>(query == "" ? "/api/runs" : $"/api/runs?{query}"))!;

    public static async Task<RunDetailResponse> GetRunAsync(this HttpClient browser, long id) =>
        (await browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{id}"))!;
}
