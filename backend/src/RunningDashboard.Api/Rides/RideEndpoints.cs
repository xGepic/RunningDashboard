using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Rides;

public static class RideEndpoints
{
    public static void MapRideEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/rides", async ([AsParameters] RideListQuery query, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = query.Validate();
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            // Filtered and sorted in memory, as the Runs are: it keeps the name search case-insensitive beyond ASCII.
            var rides = (await db.Rides
                    .Select(r => new { r.Id, r.Name, r.SportType, r.StartLocal, r.Distance, r.MovingTime, r.ElevationGain })
                    .ToListAsync(cancellationToken))
                .Select(r => new RideResponse(
                    r.Id, r.Name, r.SportType, r.StartLocal, r.Distance, r.MovingTime, Ride.SpeedOf(r.MovingTime, r.Distance), r.ElevationGain,
                    $"https://www.strava.com/activities/{r.Id}"))
                .Where(r =>
                    (string.IsNullOrWhiteSpace(query.Search) || r.Name.Contains(query.Search.Trim(), StringComparison.InvariantCultureIgnoreCase))
                    && (query.From is null || DateOnly.FromDateTime(r.StartLocal) >= query.From)
                    && (query.To is null || DateOnly.FromDateTime(r.StartLocal) <= query.To))
                .ToList();

            var page = Sorted(rides, query)
                .Skip((query.PageOrDefault - 1) * query.PageSizeOrDefault)
                .Take(query.PageSizeOrDefault)
                .ToList();

            return Results.Ok(new RidePageResponse(
                page, query.PageOrDefault, query.PageSizeOrDefault, rides.Count, RideTotalsResponse.Of(rides)));
        });
    }

    /// <summary>
    /// In the requested order; equal keys fall back to newest first, and Rides without an Avg Speed go last.
    /// </summary>
    private static IOrderedEnumerable<RideResponse> Sorted(IEnumerable<RideResponse> rides, RideListQuery query)
    {
        var ascending = query.Ascending;
        var sorted = query.SortKey switch
        {
            "distance" => ascending ? rides.OrderBy(r => r.Distance) : rides.OrderByDescending(r => r.Distance),
            "movingTime" => ascending ? rides.OrderBy(r => r.MovingTime) : rides.OrderByDescending(r => r.MovingTime),
            "speed" => rides.OrderBy(r => r.Speed is null)
                .ThenBy(r => ascending ? r.Speed : -r.Speed),
            "elevation" => ascending ? rides.OrderBy(r => r.ElevationGain) : rides.OrderByDescending(r => r.ElevationGain),
            _ => ascending ? rides.OrderBy(r => r.StartLocal) : rides.OrderByDescending(r => r.StartLocal),
        };
        return sorted.ThenByDescending(r => r.StartLocal);
    }
}
