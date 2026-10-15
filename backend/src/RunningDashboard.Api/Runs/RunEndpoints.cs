using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Fitness;
using RunningDashboard.Api.Lists;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Records;
using RunningDashboard.Api.Training;
using RunningDashboard.Api.Weather;

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

    private static readonly Dictionary<string, Func<RunExportResponse, double?>> ExportFigures = new()
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

        // Every Run the list would show, on one "page", with what a spreadsheet wants besides; the page and its size
        // are ignored.
        app.MapGet("/api/runs/export", async ([AsParameters] ListQuery query, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = query.Validate(Figures.Keys, "Runs");
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var shoeNames = await db.Shoes.ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
            var runs = (await db.Runs
                    .Select(r => new
                    {
                        r.Id, r.Name, r.SportType, r.StartLocal, r.Distance, r.MovingTime, r.ElevationGain, r.AverageHeartRate, r.Cadence,
                        r.RelativeEffort, r.ShoeId,
                    })
                    .ToListAsync(cancellationToken))
                .Where(r => query.Matches(r.Name, r.StartLocal))
                .Select(r => new RunExportResponse(
                    r.Id, r.Name, r.SportType, r.StartLocal, r.Distance, r.MovingTime, Run.PaceOf(r.MovingTime, r.Distance), r.ElevationGain,
                    r.AverageHeartRate, r.Cadence, r.RelativeEffort, r.ShoeId is null ? null : shoeNames.GetValueOrDefault(r.ShoeId),
                    $"https://www.strava.com/activities/{r.Id}"));
            return Results.Ok(query.Sorted(runs, r => r.StartLocal, ExportFigures).ToList());
        });

        // One year's Runs, road, trail and treadmill alike, added up per calendar month, by local start; all twelve
        // months, empty ones as zero.
        app.MapGet("/api/runs/months", async (int? year, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            if (year is < 1900 or > 2099)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["year"] = ["The year is between 1900 and 2099."] });

            var selectedYear = year ?? clock.GetLocalNow().Year;
            var from = new DateTime(selectedYear, 1, 1);
            var until = from.AddYears(1);
            var byMonth = (await db.Runs
                    .Where(r => r.StartLocal >= from && r.StartLocal < until)
                    .Select(r => new { r.StartLocal, r.Distance, r.MovingTime })
                    .ToListAsync(cancellationToken))
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
            var firstRun = await db.Runs.MinAsync(r => (DateTime?)r.StartLocal, cancellationToken);
            return Results.Ok(new RunMonthsResponse(selectedYear, firstRun?.Year, months));
        });

        app.MapGet("/api/runs/{id:long}", async (long id, AppDbContext db, WeatherClient weather, CancellationToken cancellationToken) =>
        {
            var run = await db.Runs
                .Include(r => r.BestEfforts)
                .Include(r => r.Splits)
                // Joined in one query, each Best Effort times each Split repeats the Run with its raw JSON.
                .AsSplitQuery()
                .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (run is null)
                return Results.NotFound();

            await LookUpTemperatureAsync(run, db, weather, cancellationToken);
            var shoe = run.ShoeId is null ? null : await db.Shoes.FindAsync([run.ShoeId], cancellationToken);
            var personalBestDistances = await PersonalBests.DistancesSetByAsync(run, db, cancellationToken);
            return Results.Ok(RunDetailResponse.From(run, shoe?.Name, personalBestDistances, await FitnessOfAsync(run, db, cancellationToken)));
        });
    }

    /// <summary>
    /// Looks the Run's temperature up the first time its page is opened and keeps it; the weather that was does not
    /// change. Without one from Open-Meteo, the next opening tries again.
    /// </summary>
    private static async Task LookUpTemperatureAsync(Run run, AppDbContext db, WeatherClient weather, CancellationToken cancellationToken)
    {
        if (run.Temperature is not null || run.WeatherLocation is not { } location)
            return;

        run.Temperature = await weather.TemperatureAtAsync(location.Latitude, location.Longitude, run.MidpointUtc, cancellationToken);
        if (run.Temperature is not null)
            await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The Run's Aerobic Efficiency, Cadence and, as its week's Long Run, Cardiac Drift; none unless it is a Fitness Run.</summary>
    private static async Task<RunFitnessResponse?> FitnessOfAsync(Run run, AppDbContext db, CancellationToken cancellationToken)
    {
        if (!FitnessFigures.IsFitnessRun(run.IsTreadmill, run.StartLocal, await FitnessEndpoints.RaceDaysAsync(db, cancellationToken)))
            return null;

        var week = TrainingWeek.Of(run.StartLocal);
        var (weekStart, weekEnd) = (week.Start.ToDateTime(TimeOnly.MinValue), week.Next.Start.ToDateTime(TimeOnly.MinValue));
        var weekRuns = await db.Runs
            .Where(r => r.StartLocal >= weekStart && r.StartLocal < weekEnd)
            .Select(r => new RunSummary(r.Id, r.Name, r.StartLocal, r.Distance, r.MovingTime, null))
            .ToListAsync(cancellationToken);
        var isLongRun = TrainingCalendar.LongRunOf(weekRuns)?.Id == run.Id;

        var splits = run.Splits.Where(s => s.Unit == SplitUnit.Kilometre).Select(FitnessFigures.From).ToList();
        return new RunFitnessResponse(
            FitnessFigures.AerobicEfficiency(run.AverageHeartRate, splits),
            run.Cadence,
            isLongRun,
            FitnessFigures.HasCardiacDrift(isLongRun, run.Distance) ? FitnessFigures.CardiacDrift(splits) : null);
    }
}
