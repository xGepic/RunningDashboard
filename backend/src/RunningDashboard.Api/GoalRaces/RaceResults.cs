using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.GoalRaces;

/// <summary>How a Goal Race went, read from the Runs of race day.</summary>
/// <param name="ElapsedTime">Seconds; none while the Run's Best Efforts are still to be fetched.</param>
public sealed record RaceResult(long RunId, string RunName, int? ElapsedTime, RaceResultSource Source);

public enum RaceResultSource
{
    /// <summary>The Run's Best Effort over the Goal Race's distance.</summary>
    BestEffort,

    /// <summary>The Run's elapsed time, as it has no Best Effort over the Goal Race's distance.</summary>
    ElapsedTime,

    /// <summary>The Run's Best Efforts are still to be fetched, so the result is not known yet.</summary>
    Pending,
}

/// <summary>
/// The Race Result (ADR 0003): from the longest Run on race day, the Best Effort over the Goal Race's distance,
/// otherwise that Run's elapsed time; with no Run on race day there is none. Of equally long Runs, the one that took
/// longer counts.
/// </summary>
public static class RaceResults
{
    /// <summary>
    /// How far (metres) a Best Effort's distance may be from the Goal Race's to count as the same: Strava's half
    /// marathon is 21,097 m, the official one 21,097.5 m.
    /// </summary>
    private const double DistanceTolerance = 1;

    public static RaceResult? Of(GoalRace race, IEnumerable<Run> runsOnRaceDay)
    {
        var run = runsOnRaceDay
            .OrderByDescending(r => r.Distance)
            .ThenByDescending(r => r.ElapsedTime)
            .ThenBy(r => r.Id)
            .FirstOrDefault();
        if (run is null)
            return null;
        if (run.DetailStatus == RunDetailStatus.Pending)
            return new RaceResult(run.Id, run.Name, null, RaceResultSource.Pending);

        var effort = run.BestEfforts.FirstOrDefault(e => Math.Abs(e.Distance - race.Distance) <= DistanceTolerance);
        return effort is null
            ? new RaceResult(run.Id, run.Name, run.ElapsedTime, RaceResultSource.ElapsedTime)
            : new RaceResult(run.Id, run.Name, effort.ElapsedTime, RaceResultSource.BestEffort);
    }

    /// <summary>The Race Result of each of <paramref name="races"/> that has one, by Goal Race id.</summary>
    public static async Task<Dictionary<int, RaceResult>> OfAsync(IReadOnlyCollection<GoalRace> races, AppDbContext db, CancellationToken cancellationToken)
    {
        if (races.Count == 0)
            return [];

        var raceDays = races.Select(r => r.Date).ToHashSet();
        var from = raceDays.Min().ToDateTime(TimeOnly.MinValue);
        var until = raceDays.Max().AddDays(1).ToDateTime(TimeOnly.MinValue);
        var runs = (await db.Runs
                .Include(r => r.BestEfforts)
                .Where(r => r.StartLocal >= from && r.StartLocal < until)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .ToLookup(r => DateOnly.FromDateTime(r.StartLocal));

        return races
            .Select(race => (race.Id, Result: Of(race, runs[race.Date])))
            .Where(r => r.Result is not null)
            .ToDictionary(r => r.Id, r => r.Result!);
    }
}
