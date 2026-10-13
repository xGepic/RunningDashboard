using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Lists;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Swims;

public static class SwimEndpoints
{
    /// <summary>What the Swims list sorts by, besides date.</summary>
    private static readonly Dictionary<string, Func<SwimResponse, double?>> Figures = new()
    {
        ["distance"] = s => s.Distance,
        ["movingTime"] = s => s.MovingTime,
        ["pace"] = s => s.Pace,
    };

    public static void MapSwimEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/swims", async ([AsParameters] ListQuery query, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = query.Validate(Figures.Keys, "Swims");
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            // Filtered and sorted in memory, as the Runs are: it keeps the name search case-insensitive beyond ASCII.
            var swims = (await db.ListedActivities
                    .Where(a => Sports.Swims.Contains(a.SportType))
                    .Select(a => new { a.Id, a.Name, a.StartLocal, a.Distance, a.MovingTime })
                    .ToListAsync(cancellationToken))
                .Select(a => new SwimResponse(
                    a.Id, a.Name, a.StartLocal, a.Distance, a.MovingTime, ListedActivity.SwimPaceOf(a.MovingTime, a.Distance),
                    $"https://www.strava.com/activities/{a.Id}"))
                .Where(s => query.Matches(s.Name, s.StartLocal))
                .ToList();

            return Results.Ok(new SwimPageResponse(
                query.PageOf(swims, s => s.StartLocal, Figures), query.PageOrDefault, query.PageSizeOrDefault, swims.Count, SwimTotalsResponse.Of(swims)));
        });
    }
}
