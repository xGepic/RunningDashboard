using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Overview;

public static class OverviewEndpoints
{
    public static void MapOverviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/overview", async (AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var runs = await db.Runs
                .Select(r => new RunFigures(r.StartLocal, r.Distance, r.MovingTime, r.ElevationGain))
                .ToListAsync(cancellationToken);
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var goal = await db.GetYearlyGoalAsync(today.Year, cancellationToken);
            return OverviewResponse.From(runs, goal is null ? null : YearlyGoalResponse.From(goal), today);
        });
    }
}
