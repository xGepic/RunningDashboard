using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Lists;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Records;

namespace RunningDashboard.Api.Runs;

public static class RunEndpoints
{
    /// <summary>What the Runs list sorts by, besides date.</summary>
    private static readonly Dictionary<string, Func<RunResponse, double?>> Figures = new()
    {
        ["distance"] = r => r.Distance,
        ["movingTime"] = r => r.MovingTime,
        ["pace"] = r => r.Pace,
    };

    public static void MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/runs", async ([AsParameters] ListQuery query, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = query.Validate(Figures.Keys, "Runs");
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

            var matching = runs.Where(r => query.Matches(r.Name, r.StartLocal)).ToList();
            var page = query.PageOf(matching, r => r.StartLocal, Figures);
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
}
