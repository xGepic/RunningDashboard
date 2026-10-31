using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Runs;

/// <param name="FirstYear">The year of the first Run; none without Runs.</param>
/// <param name="Weekdays">Monday to Sunday; days without Runs as zero.</param>
internal sealed record RunMonthsResponse(int Year, int? FirstYear, IReadOnlyList<MonthTotalsResponse> Months, IReadOnlyList<WeekdayTotalsResponse> Weekdays);

/// <param name="Day">"monday" to "sunday", by the Runs' local start.</param>
/// <param name="Distance">Metres.</param>
internal sealed record WeekdayTotalsResponse(string Day, int Runs, double Distance);

/// <param name="Month">1 for January.</param>
/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="Pace">Seconds per kilometre over all of the month's Runs; none without Runs.</param>
internal sealed record MonthTotalsResponse(int Month, int Runs, double Distance, int MovingTime, double? Pace);

internal sealed record RunPageResponse(IReadOnlyList<RunResponse> Items, int Page, int PageSize, int TotalCount);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="Pace">Seconds per kilometre.</param>
/// <param name="ShoeName">Name of the Shoe logged with the Run, if Strava still lists it.</param>
internal sealed record RunResponse(long Id, string Name, string SportType, DateTime StartLocal, double Distance, int MovingTime, double? Pace, string? ShoeName);

/// <summary>A Run as exported: what the list shows, and more.</summary>
/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="Pace">Seconds per kilometre.</param>
/// <param name="ElevationGain">Metres.</param>
/// <param name="AverageHeartRate">Beats per minute.</param>
/// <param name="Cadence">Steps per minute, both feet together.</param>
internal sealed record RunExportResponse(
    long Id,
    string Name,
    string SportType,
    DateTime StartLocal,
    double Distance,
    int MovingTime,
    double? Pace,
    double ElevationGain,
    double? AverageHeartRate,
    double? Cadence,
    double? RelativeEffort,
    string? ShoeName,
    string StravaUrl);

/// <param name="AverageHeartRate">Beats per minute; none without a heart-rate recording.</param>
/// <param name="Detail">"pending", "loaded" or "unavailable": whether Best Efforts and splits are in.</param>
/// <param name="Route">Google's encoded polyline of the route; none for a Run without GPS.</param>
/// <param name="MileSplits">Strava's splits per mile, for an Athlete who reads miles; Pace is still per kilometre.</param>
internal sealed record RunDetailResponse(
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
    IReadOnlyList<BestEffortResponse> BestEfforts,
    IReadOnlyList<SplitResponse> Splits,
    IReadOnlyList<SplitResponse> MileSplits,
    RunFitnessResponse? Fitness)
{
    /// <param name="personalBestDistances">Distances at which the Run's Best Effort was a Personal Best at the time.</param>
    /// <param name="fitness">None unless it is a Fitness Run.</param>
    public static RunDetailResponse From(Run run, string? shoeName, HashSet<double> personalBestDistances, RunFitnessResponse? fitness) => new(
        run.Id,
        run.Name,
        run.SportType,
        run.StartLocal,
        run.Distance,
        run.MovingTime,
        run.ElapsedTime,
        Run.PaceOf(run.MovingTime, run.Distance),
        run.ElevationGain,
        run.RelativeEffort,
        run.AverageHeartRate,
        $"https://www.strava.com/activities/{run.Id}",
        shoeName,
        run.DetailStatus.ToString().ToLowerInvariant(),
        run.Route,
        [.. run.BestEfforts
            .OrderBy(e => e.Distance)
            .Select(e => new BestEffortResponse(
                e.Name, e.Distance, e.ElapsedTime, Run.PerKilometre(e.ElapsedTime, e.Distance), personalBestDistances.Contains(e.Distance)))],
        SplitResponse.Of(run, SplitUnit.Kilometre),
        SplitResponse.Of(run, SplitUnit.Mile),
        fitness);
}

/// <param name="Temperature">°C halfway through the Run; none without a start location or while Open-Meteo has none.</param>
internal sealed record RunTemperatureResponse(double? Temperature);

/// <summary>A Fitness Run's figures.</summary>
/// <param name="AerobicEfficiency">Metres per heartbeat; none without heart rate.</param>
/// <param name="Cadence">Steps per minute, both feet together.</param>
/// <param name="IsLongRun">Whether it is its Training Week's Long Run.</param>
/// <param name="CardiacDrift">A fraction, 0.05 being 5%; only for a Long Run of at least 10 km.</param>
internal sealed record RunFitnessResponse(double? AerobicEfficiency, double? Cadence, bool IsLongRun, double? CardiacDrift);

/// <param name="TimePerKm">Elapsed time per kilometre, in seconds. Not Pace, which is Moving Time based.</param>
/// <param name="PersonalBest">Whether it was a Personal Best when it was run: faster than every earlier Best Effort.</param>
internal sealed record BestEffortResponse(string Name, double Distance, int ElapsedTime, double? TimePerKm, bool PersonalBest);

/// <param name="Pace">Per kilometre, for mile splits too.</param>
internal sealed record SplitResponse(int Index, double Distance, int MovingTime, double? Pace, double ElevationDifference)
{
    /// <summary>The Run's splits of <paramref name="unit"/>, in order.</summary>
    public static List<SplitResponse> Of(Run run, SplitUnit unit) =>
        [.. run.Splits
            .Where(s => s.Unit == unit)
            .OrderBy(s => s.Index)
            .Select(s => new SplitResponse(s.Index, s.Distance, s.MovingTime, Run.PaceOf(s.MovingTime, s.Distance), s.ElevationDifference))];
}
