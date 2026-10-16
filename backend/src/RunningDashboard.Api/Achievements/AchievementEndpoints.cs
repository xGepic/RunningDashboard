using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Records;

namespace RunningDashboard.Api.Achievements;

public static class AchievementEndpoints
{
    public static void MapAchievementEndpoints(this IEndpointRouteBuilder app)
    {
        // Worked out from the stored Runs on every request: a Run deleted on Strava takes its Achievements with it.
        app.MapGet("/api/achievements", async (AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var runs = await db.Runs
                .Select(r => new AchievementRun(r.Id, r.Name, r.StartUtc, r.StartLocal, r.Distance))
                .ToListAsync(cancellationToken);
            var efforts = await PersonalBests.BestEfforts(db, AchievementRules.SingleRunDistances).ToListAsync(cancellationToken);
            return AchievementRules.Of(runs, efforts, today);
        });
    }
}
