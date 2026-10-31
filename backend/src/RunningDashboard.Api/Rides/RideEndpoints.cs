using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Lists;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Strava;

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

    /// <summary>The ?kind= that lists only the outdoor Rides; without a kind, all of them are listed.</summary>
    private const string Outdoor = "outdoor";

    /// <summary>The ?kind= that lists only the virtual Rides.</summary>
    private const string Virtual = "virtual";

    public static void MapRideEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/rides", async ([AsParameters] ListQuery query, string? kind, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = Validate(query, kind);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var rides = await MatchingAsync(query, kind, db, cancellationToken);
            return Results.Ok(new RidePageResponse(
                query.PageOf(rides, r => r.StartLocal, Figures), query.PageOrDefault, query.PageSizeOrDefault, rides.Count, RideTotalsResponse.Of(rides)));
        });

        // Every Ride the list would show, on one "page"; the page and its size are ignored.
        app.MapGet("/api/rides/export", async ([AsParameters] ListQuery query, string? kind, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = Validate(query, kind);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            return Results.Ok(query.Sorted(await MatchingAsync(query, kind, db, cancellationToken), r => r.StartLocal, Figures).ToList());
        });

        app.MapGet("/api/rides/{id:long}", async (long id, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var ride = await db.ListedActivities.SingleOrDefaultAsync(a => a.Id == id && Sports.Rides.Contains(a.SportType), cancellationToken);
            return ride is null ? Results.NotFound() : Results.Ok(RideDetailResponse.From(ride));
        });
    }

    private static Dictionary<string, string[]> Validate(ListQuery query, string? kind)
    {
        var errors = query.Validate(Figures.Keys, "Rides");
        if (kind is not (null or Outdoor or Virtual))
            errors[nameof(kind)] = [$"The kind is {Outdoor} or {Virtual}, or none for all Rides."];
        return errors;
    }

    private static bool IsOfKind(string sportType, string? kind) => kind switch
    {
        Outdoor => sportType != Sports.VirtualRide,
        Virtual => sportType == Sports.VirtualRide,
        _ => true,
    };

    /// <summary>
    /// The Rides of the kind (all without one) matching the search and date range, unsorted. Filtered in memory, as the Runs are: it keeps the name
    /// search case-insensitive beyond ASCII.
    /// </summary>
    private static async Task<List<RideResponse>> MatchingAsync(ListQuery query, string? kind, AppDbContext db, CancellationToken cancellationToken) =>
        [.. (await db.ListedActivities
            .Where(a => Sports.Rides.Contains(a.SportType))
            .Select(a => new { a.Id, a.Name, a.SportType, a.StartLocal, a.Distance, a.MovingTime, a.ElevationGain })
            .ToListAsync(cancellationToken))
        .Select(a => new RideResponse(
            a.Id, a.Name, a.SportType, a.StartLocal, a.Distance, a.MovingTime, ListedActivity.AvgSpeedOf(a.MovingTime, a.Distance), a.ElevationGain,
            StravaLinks.Activity(a.Id)))
        .Where(r => IsOfKind(r.SportType, kind) && query.Matches(r.Name, r.StartLocal))];
}
