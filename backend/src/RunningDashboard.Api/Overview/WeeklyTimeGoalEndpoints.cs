using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Overview;

/// <summary>Sets and removes the Weekly Time Goal; GET /api/overview shows it with this week's Training Time.</summary>
public static class WeeklyTimeGoalEndpoints
{
    /// <summary>100 hours: far beyond what anyone trains in a week.</summary>
    private const int MaxTime = 100 * 3600;

    public static void MapWeeklyTimeGoalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/weekly-time-goal", async (WeeklyTimeGoalRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (request.Time is not (> 0 and <= MaxTime))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["time"] = [$"The goal is a positive number of seconds, at most {MaxTime / 3600} hours."],
                });

            var goal = await db.WeeklyTimeGoals.SingleOrDefaultAsync(cancellationToken);
            if (goal is null)
            {
                goal = new WeeklyTimeGoal();
                db.WeeklyTimeGoals.Add(goal);
            }

            goal.Time = request.Time.Value;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(WeeklyTimeGoalResponse.From(goal));
        });

        app.MapDelete("/api/weekly-time-goal", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            await db.WeeklyTimeGoals.ExecuteDeleteAsync(cancellationToken);
            return Results.NoContent();
        });
    }
}
