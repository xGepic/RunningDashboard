using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Records;

namespace RunningDashboard.Api.Runs;

public static class RunEndpoints
{
    public static void MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/runs", async ([AsParameters] RunListQuery query, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = query.Validate();
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            // Filtered and sorted in memory over a lean projection: a few thousand Runs at most, and it keeps the
            // name search case-insensitive beyond ASCII (SQLite's LIKE is not), independent of the server's culture.
            var shoeNames = await db.Shoes.ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
            var runs = (await db.Runs
                    .Select(r => new { r.Id, r.Name, r.SportType, r.StartLocal, r.Distance, r.MovingTime, r.ShoeId })
                    .ToListAsync(cancellationToken))
                .Select(r => new RunResponse(
                    r.Id, r.Name, r.SportType, r.StartLocal, r.Distance, r.MovingTime, Run.PaceOf(r.MovingTime, r.Distance),
                    r.ShoeId is null ? null : shoeNames.GetValueOrDefault(r.ShoeId)))
                .ToList();

            var matching = runs.Where(r =>
                (string.IsNullOrWhiteSpace(query.Search) || r.Name.Contains(query.Search.Trim(), StringComparison.InvariantCultureIgnoreCase))
                && (query.From is null || DateOnly.FromDateTime(r.StartLocal) >= query.From)
                && (query.To is null || DateOnly.FromDateTime(r.StartLocal) <= query.To)
                && (query.SportType is null || r.SportType == query.SportType))
                .ToList();

            var page = Sorted(matching, query)
                .Skip((query.PageOrDefault - 1) * query.PageSizeOrDefault)
                .Take(query.PageSizeOrDefault)
                .ToList();

            return Results.Ok(new RunPageResponse(page, query.PageOrDefault, query.PageSizeOrDefault, matching.Count));
        });

        app.MapGet("/api/runs/{id:long}", async (long id, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var run = await db.Runs
                .Include(r => r.BestEfforts)
                .Include(r => r.Splits)
                .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (run is null)
                return Results.NotFound();

            var shoe = run.ShoeId is null ? null : await db.Shoes.FindAsync([run.ShoeId], cancellationToken);
            var personalBestDistances = await PersonalBests.DistancesSetByAsync(run, db, cancellationToken);
            return Results.Ok(RunDetailResponse.From(run, shoe?.Name, personalBestDistances));
        });
    }

    /// <summary>
    /// In the requested order; equal keys fall back to newest first, and Runs without a Pace go last.
    /// </summary>
    private static IOrderedEnumerable<RunResponse> Sorted(IEnumerable<RunResponse> runs, RunListQuery query)
    {
        var ascending = query.Ascending;
        var sorted = query.SortKey switch
        {
            "distance" => ascending ? runs.OrderBy(r => r.Distance) : runs.OrderByDescending(r => r.Distance),
            "movingTime" => ascending ? runs.OrderBy(r => r.MovingTime) : runs.OrderByDescending(r => r.MovingTime),
            "pace" => runs.OrderBy(r => r.Pace is null)
                .ThenBy(r => ascending ? r.Pace : -r.Pace),
            _ => ascending ? runs.OrderBy(r => r.StartLocal) : runs.OrderByDescending(r => r.StartLocal),
        };
        return sorted.ThenByDescending(r => r.StartLocal);
    }

    private sealed record RunPageResponse(IReadOnlyList<RunResponse> Items, int Page, int PageSize, int TotalCount);

    /// <param name="Distance">Metres.</param>
    /// <param name="MovingTime">Seconds.</param>
    /// <param name="Pace">Seconds per kilometre.</param>
    /// <param name="ShoeName">Name of the Shoe logged with the Run, if Strava still lists it.</param>
    private sealed record RunResponse(long Id, string Name, string SportType, DateTime StartLocal, double Distance, int MovingTime, double? Pace, string? ShoeName);

    /// <param name="Detail">"pending", "loaded" or "unavailable": whether Best Efforts and splits are in.</param>
    /// <param name="Route">Google's encoded polyline of the route; none for a Run without GPS.</param>
    private sealed record RunDetailResponse(
        long Id,
        string Name,
        string SportType,
        DateTime StartLocal,
        double Distance,
        int MovingTime,
        int ElapsedTime,
        double? Pace,
        double ElevationGain,
        double? RelativeEffort,
        string StravaUrl,
        string? ShoeName,
        string Detail,
        string? Route,
        IReadOnlyList<BestEffortResponse> BestEfforts,
        IReadOnlyList<SplitResponse> Splits)
    {
        /// <param name="personalBestDistances">Distances at which the Run's Best Effort was a Personal Best at the time.</param>
        public static RunDetailResponse From(Run run, string? shoeName, HashSet<double> personalBestDistances) => new(
            run.Id,
            run.Name,
            run.SportType,
            run.StartLocal,
            run.Distance,
            run.MovingTime,
            run.ElapsedTime,
            Run.PaceOf(run.MovingTime, run.Distance),
            run.ElevationGain,
            run.RelativeEffort,
            $"https://www.strava.com/activities/{run.Id}",
            shoeName,
            run.DetailStatus.ToString().ToLowerInvariant(),
            run.Route,
            [.. run.BestEfforts
                .OrderBy(e => e.Distance)
                .Select(e => new BestEffortResponse(
                    e.Name, e.Distance, e.ElapsedTime, Run.PerKilometre(e.ElapsedTime, e.Distance), personalBestDistances.Contains(e.Distance)))],
            [.. run.Splits
                .OrderBy(s => s.Index)
                .Select(s => new SplitResponse(s.Index, s.Distance, s.MovingTime, Run.PaceOf(s.MovingTime, s.Distance), s.ElevationDifference))]);
    }

    /// <param name="TimePerKm">Elapsed time per kilometre, in seconds. Not Pace, which is Moving Time based.</param>
    /// <param name="PersonalBest">Whether it was a Personal Best when it was run: faster than every earlier Best Effort.</param>
    private sealed record BestEffortResponse(string Name, double Distance, int ElapsedTime, double? TimePerKm, bool PersonalBest);

    private sealed record SplitResponse(int Index, double Distance, int MovingTime, double? Pace, double ElevationDifference);
}
