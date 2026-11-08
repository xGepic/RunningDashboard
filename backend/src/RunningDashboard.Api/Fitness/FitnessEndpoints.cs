using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Fitness;

public static class FitnessEndpoints
{
    public static void MapFitnessEndpoints(this IEndpointRouteBuilder app)
    {
        // Every Fitness Run's Cadence over the whole history, with the monthly averages. A Run's Aerobic Efficiency and
        // Cardiac Drift are on its own page, from the run details.
        app.MapGet("/api/fitness", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var runs = await db.Runs
                .Select(r => new FitnessRunData(r.Id, r.Name, r.SportType, r.Trainer, r.StartLocal, r.Distance, r.Cadence))
                .ToListAsync(cancellationToken);
            var raceDays = await db.GetRaceDaysAsync(cancellationToken);

            var points = runs
                .Where(r => FitnessFigures.IsFitnessRun(r.IsTreadmill, r.StartLocal, raceDays))
                .OrderBy(r => r.StartLocal)
                .Select(r => new FitnessRunResponse(r.Id, r.Name, r.StartLocal, r.Distance, r.Cadence))
                .ToList();

            return new FitnessResponse(points, FitnessMonthResponse.Of(points));
        });

        // Fitness & Freshness from every Run's Relative Effort, treadmill and race day alike: they load the body all
        // the same. Rides and Swims count toward nothing. A Run without Relative Effort (recorded without heart rate)
        // adds no load.
        app.MapGet("/api/fitness/freshness", async (AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var runs = await db.Runs.Select(r => new { r.Id, r.Name, r.StartLocal, r.RelativeEffort }).ToListAsync(cancellationToken);
            var byDay = runs.GroupBy(r => DateOnly.FromDateTime(r.StartLocal)).ToList();
            var loadByDay = byDay.ToDictionary(g => g.Key, g => g.Sum(r => r.RelativeEffort ?? 0));
            // Of equally hard Runs, the first.
            var hardestByDay = byDay.ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.RelativeEffort ?? 0).ThenBy(r => r.StartLocal).ThenBy(r => r.Id).Select(r => (r.Id, r.Name)).First());
            var races = await db.GoalRaces
                .OrderBy(r => r.Date)
                .Select(r => new FreshnessRaceResponse(r.Date, r.Name))
                .ToListAsync(cancellationToken);

            var days = FitnessFreshness.Of(loadByDay, clock.Today())
                .Select(day => FreshnessDayResponse.From(day, hardestByDay.TryGetValue(day.Date, out var run) ? run : null))
                .ToList();
            return new FreshnessResponse(days, races);
        });
    }

    private sealed record FitnessRunData(long Id, string Name, string SportType, bool Trainer, DateTime StartLocal, double Distance, double? Cadence)
    {
        public bool IsTreadmill => Run.IsTreadmillRun(Trainer, SportType);
    }
}
