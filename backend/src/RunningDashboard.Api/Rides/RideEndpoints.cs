using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Lists;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Rides;

public static class RideEndpoints
{
    /// <summary>What the Rides list sorts by, besides date.</summary>
    private static readonly Dictionary<string, Func<RideResponse, double?>> Figures = new()
    {
        ["distance"] = r => r.Distance,
        ["movingTime"] = r => r.MovingTime,
        ["speed"] = r => r.Speed,
        ["elevation"] = r => r.ElevationGain,
    };

    public static void MapRideEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/rides", async ([AsParameters] ListQuery query, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = query.Validate(Figures.Keys, "Rides");
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var rides = await MatchingAsync(query, db, cancellationToken);
            return Results.Ok(new RidePageResponse(
                query.PageOf(rides, r => r.StartLocal, Figures), query.PageOrDefault, query.PageSizeOrDefault, rides.Count, RideTotalsResponse.Of(rides)));
        });

        // Every Ride the list would show, on one "page"; the page and its size are ignored.
        app.MapGet("/api/rides/export", async ([AsParameters] ListQuery query, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = query.Validate(Figures.Keys, "Rides");
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            return Results.Ok(query.Sorted(await MatchingAsync(query, db, cancellationToken), r => r.StartLocal, Figures).ToList());
        });
    }

    /// <summary>
    /// The Rides matching the search and date range, unsorted. Filtered in memory, as the Runs are: it keeps the name
    /// search case-insensitive beyond ASCII.
    /// </summary>
    private static async Task<List<RideResponse>> MatchingAsync(ListQuery query, AppDbContext db, CancellationToken cancellationToken) =>
        (await db.ListedActivities
            .Where(a => Sports.Rides.Contains(a.SportType))
            .Select(a => new { a.Id, a.Name, a.SportType, a.StartLocal, a.Distance, a.MovingTime, a.ElevationGain })
            .ToListAsync(cancellationToken))
        .Select(a => new RideResponse(
            a.Id, a.Name, a.SportType, a.StartLocal, a.Distance, a.MovingTime, ListedActivity.AvgSpeedOf(a.MovingTime, a.Distance), a.ElevationGain,
            $"https://www.strava.com/activities/{a.Id}"))
        .Where(r => query.Matches(r.Name, r.StartLocal))
        .ToList();
}
