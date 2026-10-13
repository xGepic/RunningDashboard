using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Swims;

public static class SwimEndpoints
{
    public static void MapSwimEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/swims", async ([AsParameters] SwimListQuery query, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = query.Validate();
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            // Filtered and sorted in memory, as the Runs are: it keeps the name search case-insensitive beyond ASCII.
            var swims = (await db.Swims
                    .Select(s => new { s.Id, s.Name, s.StartLocal, s.Distance, s.MovingTime })
                    .ToListAsync(cancellationToken))
                .Select(s => new SwimResponse(
                    s.Id, s.Name, s.StartLocal, s.Distance, s.MovingTime, Swim.PaceOf(s.MovingTime, s.Distance),
                    $"https://www.strava.com/activities/{s.Id}"))
                .Where(s =>
                    (string.IsNullOrWhiteSpace(query.Search) || s.Name.Contains(query.Search.Trim(), StringComparison.InvariantCultureIgnoreCase))
                    && (query.From is null || DateOnly.FromDateTime(s.StartLocal) >= query.From)
                    && (query.To is null || DateOnly.FromDateTime(s.StartLocal) <= query.To))
                .ToList();

            var page = Sorted(swims, query)
                .Skip((query.PageOrDefault - 1) * query.PageSizeOrDefault)
                .Take(query.PageSizeOrDefault)
                .ToList();

            return Results.Ok(new SwimPageResponse(
                page, query.PageOrDefault, query.PageSizeOrDefault, swims.Count, SwimTotalsResponse.Of(swims)));
        });
    }

    /// <summary>
    /// In the requested order; equal keys fall back to newest first, and Swims without a Swim Pace go last.
    /// </summary>
    private static IOrderedEnumerable<SwimResponse> Sorted(IEnumerable<SwimResponse> swims, SwimListQuery query)
    {
        var ascending = query.Ascending;
        var sorted = query.SortKey switch
        {
            "distance" => ascending ? swims.OrderBy(s => s.Distance) : swims.OrderByDescending(s => s.Distance),
            "movingTime" => ascending ? swims.OrderBy(s => s.MovingTime) : swims.OrderByDescending(s => s.MovingTime),
            "pace" => swims.OrderBy(s => s.Pace is null)
                .ThenBy(s => ascending ? s.Pace : -s.Pace),
            _ => ascending ? swims.OrderBy(s => s.StartLocal) : swims.OrderByDescending(s => s.StartLocal),
        };
        return sorted.ThenByDescending(s => s.StartLocal);
    }
}
