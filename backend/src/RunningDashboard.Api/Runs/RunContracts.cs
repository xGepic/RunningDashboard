using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Runs;

/// <param name="FirstYear">The year of the first Run; none without Runs.</param>
internal sealed record RunMonthsResponse(int Year, int? FirstYear, IReadOnlyList<MonthTotalsResponse> Months);

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

/// <param name="Detail">"pending", "loaded" or "unavailable": whether Best Efforts and splits are in.</param>
/// <param name="Route">Google's encoded polyline of the route; none for a Run without GPS.</param>
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
    string StravaUrl,
    string? ShoeName,
    string Detail,
    string? Route,
    IReadOnlyList<BestEffortResponse> BestEfforts,
    IReadOnlyList<SplitResponse> Splits)
{
    /// <param name="personalBestDistances">Distances at which the Run's Best Effort was a Personal Best at the time.</param>
    public static RunDetailResponse From(Run run, string? shoeName, HashSet<double> personalBestDistances) => new(
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
        $"https://www.strava.com/activities/{run.Id}",
        shoeName,
        run.DetailStatus.ToString().ToLowerInvariant(),
        run.Route,
        [.. run.BestEfforts
            .OrderBy(e => e.Distance)
            .Select(e => new BestEffortResponse(
                e.Name, e.Distance, e.ElapsedTime, Run.PerKilometre(e.ElapsedTime, e.Distance), personalBestDistances.Contains(e.Distance)))],
        [.. run.Splits
            .OrderBy(s => s.Index)
            .Select(s => new SplitResponse(s.Index, s.Distance, s.MovingTime, Run.PaceOf(s.MovingTime, s.Distance), s.ElevationDifference))]);
}

/// <param name="TimePerKm">Elapsed time per kilometre, in seconds. Not Pace, which is Moving Time based.</param>
/// <param name="PersonalBest">Whether it was a Personal Best when it was run: faster than every earlier Best Effort.</param>
internal sealed record BestEffortResponse(string Name, double Distance, int ElapsedTime, double? TimePerKm, bool PersonalBest);

internal sealed record SplitResponse(int Index, double Distance, int MovingTime, double? Pace, double ElevationDifference);
