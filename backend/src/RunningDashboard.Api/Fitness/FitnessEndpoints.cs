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
    }

    private sealed record FitnessRunData(long Id, string Name, string SportType, bool Trainer, DateTime StartLocal, double Distance, double? Cadence)
    {
        public bool IsTreadmill => Run.IsTreadmillRun(Trainer, SportType);
    }
}
