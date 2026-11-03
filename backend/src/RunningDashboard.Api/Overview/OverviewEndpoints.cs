using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Journey;
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
            var listed = await db.ListedActivities
                .Select(a => new ListedFigures(a.StartLocal, a.MovingTime, a.SportType))
                .ToListAsync(cancellationToken);
            var today = clock.Today();
            var goal = await db.GetYearlyGoalAsync(today.Year, cancellationToken);
            var weeklyGoal = await db.WeeklyTimeGoals.SingleOrDefaultAsync(cancellationToken);
            // On 29 February, 28 February a year ago.
            var yearAgo = today.AddYears(-1);
            var yearAgoStart = yearAgo.ToDateTime(TimeOnly.MinValue);
            var runsYearAgo = await db.Runs
                .Where(r => r.StartLocal >= yearAgoStart && r.StartLocal < yearAgoStart.AddDays(1))
                .OrderByDescending(r => r.StartLocal)
                .Select(r => new OnThisDayRunResponse(r.Id, r.Name, r.StartLocal, r.Distance, r.MovingTime))
                .ToListAsync(cancellationToken);
            var journey = await VirtualJourney.ForYearAsync(db, today.Year, today.Year, cancellationToken);
            return OverviewResponse.From(
                runs,
                listed,
                goal is null ? null : YearlyGoalResponse.From(goal),
                weeklyGoal is null ? null : WeeklyTimeGoalResponse.From(weeklyGoal),
                runsYearAgo.Count == 0 ? null : new OnThisDayResponse(yearAgo, runsYearAgo),
                journey,
                today);
        });
    }
}
