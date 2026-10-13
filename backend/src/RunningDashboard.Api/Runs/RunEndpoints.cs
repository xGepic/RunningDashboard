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
                && (query.To is null || DateOnly.FromDateTime(r.StartLocal) <= query.To))
                .ToList();

            var page = Sorted(matching, query)
                .Skip((query.PageOrDefault - 1) * query.PageSizeOrDefault)
                .Take(query.PageSizeOrDefault)
                .ToList();

            return Results.Ok(new RunPageResponse(page, query.PageOrDefault, query.PageSizeOrDefault, matching.Count));
        });

        // One year's Runs, road, trail and treadmill alike, added up per calendar month, by local start; all twelve
        // months, empty ones as zero.
        app.MapGet("/api/runs/months", async (int? year, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            if (year is < 1900 or > 2099)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["year"] = ["The year is between 1900 and 2099."] });

            var selectedYear = year ?? clock.GetLocalNow().Year;
            var runs = await db.Runs
                .Select(r => new { r.StartLocal, r.Distance, r.MovingTime })
                .ToListAsync(cancellationToken);
            var byMonth = runs
                .Where(r => r.StartLocal.Year == selectedYear)
                .ToLookup(r => r.StartLocal.Month);
            var months = Enumerable.Range(1, 12)
                .Select(month =>
                {
                    var inMonth = byMonth[month].ToList();
                    var distance = inMonth.Sum(r => r.Distance);
                    var movingTime = inMonth.Sum(r => r.MovingTime);
                    return new MonthTotalsResponse(month, inMonth.Count, distance, movingTime, Run.PaceOf(movingTime, distance));
                })
                .ToList();
            int? firstYear = runs.Count == 0 ? null : runs.Min(r => r.StartLocal.Year);
            return Results.Ok(new RunMonthsResponse(selectedYear, firstYear, months));
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
}
