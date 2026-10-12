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
        // Always for the current year: a goal left from an earlier year is replaced.
        app.MapPut("/api/yearly-goal", async (YearlyGoalRequest request, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            if (request.Distance is not (> 0 and <= MaxDistance))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["distance"] = [$"The goal is a positive number of metres, at most {MaxDistance / 1000:0} km."],
                });

            var goal = await db.YearlyGoals.SingleOrDefaultAsync(cancellationToken);
            if (goal is null)
            {
                goal = new YearlyGoal();
                db.YearlyGoals.Add(goal);
            }

            goal.Year = clock.GetLocalNow().Year;
            goal.Distance = request.Distance.Value;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(YearlyGoalResponse.From(goal));
        });

        app.MapDelete("/api/yearly-goal", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            await db.YearlyGoals.ExecuteDeleteAsync(cancellationToken);
            return Results.NoContent();
        });
    }

    /// <param name="Distance">Metres.</param>
    private sealed record YearlyGoalRequest(double? Distance);
}

/// <param name="Distance">Metres.</param>
internal sealed record YearlyGoalResponse(int Year, double Distance)
{
    public static YearlyGoalResponse From(YearlyGoal goal) => new(goal.Year, goal.Distance);
}
