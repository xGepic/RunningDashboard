using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Overview;

/// <summary>Sets and removes the Yearly Goal; GET /api/overview shows it with the year's distance.</summary>
public static class YearlyGoalEndpoints
{
    /// <summary>Far beyond what anyone runs in a year.</summary>
    private const double MaxDistance = 20_000_000;

    public static void MapYearlyGoalEndpoints(this IEndpointRouteBuilder app)
    {
        // Always for the current year; earlier years' goals are kept, but no longer count.
        app.MapPut("/api/yearly-goal", async (YearlyGoalRequest request, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            if (request.Distance is not (> 0 and <= MaxDistance))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["distance"] = [$"The goal is a positive number of metres, at most {MaxDistance / 1000:0} km."],
                });

            var year = clock.Today().Year;
            var goal = await db.GetYearlyGoalAsync(year, cancellationToken);
            if (goal is null)
            {
                goal = new YearlyGoal { Year = year };
                db.YearlyGoals.Add(goal);
            }

            goal.Distance = request.Distance.Value;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(YearlyGoalResponse.From(goal));
        });

        // The current year's only.
        app.MapDelete("/api/yearly-goal", async (AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var year = clock.Today().Year;
            await db.YearlyGoals.Where(g => g.Year == year).ExecuteDeleteAsync(cancellationToken);
            return Results.NoContent();
        });
    }
}
