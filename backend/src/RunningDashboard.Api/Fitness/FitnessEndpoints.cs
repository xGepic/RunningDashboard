using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Training;

namespace RunningDashboard.Api.Fitness;

public static class FitnessEndpoints
{
    public static void MapFitnessEndpoints(this IEndpointRouteBuilder app)
    {
        // Every Fitness Run's Aerobic Efficiency, Cardiac Drift and Cadence over the whole history, with their monthly averages.
        app.MapGet("/api/fitness", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var runs = await db.Runs
                .Select(r => new FitnessRunData(r.Id, r.Name, r.SportType, r.Trainer, r.StartLocal, r.Distance, r.MovingTime, r.AverageHeartRate, r.Cadence))
                .ToListAsync(cancellationToken);
            var splits = (await db.Set<Split>()
                    .Where(s => s.Unit == SplitUnit.Kilometre)
                    .Select(s => new { s.RunId, Split = new FitnessSplit(s.Index, s.Distance, s.MovingTime, s.HeartRate, s.GradeAdjustedSpeed) })
                    .ToListAsync(cancellationToken))
                .ToLookup(s => s.RunId, s => s.Split);
            var raceDays = await db.GetRaceDaysAsync(cancellationToken);

            var longRuns = runs
                .GroupBy(r => TrainingWeek.Of(r.StartLocal))
                .Select(g => TrainingCalendar.LongRunOf(g.Select(r => new RunSummary(r.Id, r.Name, r.StartLocal, r.Distance, r.MovingTime, null)))!.Id)
                .ToHashSet();

            var points = runs
                .Where(r => FitnessFigures.IsFitnessRun(r.IsTreadmill, r.StartLocal, raceDays))
                .OrderBy(r => r.StartLocal)
                .Select(r =>
                {
                    var runSplits = splits[r.Id].ToList();
                    var drift = FitnessFigures.HasCardiacDrift(longRuns.Contains(r.Id), r.Distance) ? FitnessFigures.CardiacDrift(runSplits) : null;
                    return new FitnessRunResponse(r.Id, r.Name, r.StartLocal, r.Distance, FitnessFigures.AerobicEfficiency(r.AverageHeartRate, runSplits), drift, r.Cadence);
                })
                .ToList();

            return new FitnessResponse(FitnessFigures.GoodCardiacDrift, points, FitnessMonthResponse.Of(points));
        });
    }

    private sealed record FitnessRunData(
        long Id, string Name, string SportType, bool Trainer, DateTime StartLocal, double Distance, int MovingTime, double? AverageHeartRate, double? Cadence)
    {
        public bool IsTreadmill => Run.IsTreadmillRun(Trainer, SportType);
    }
}
