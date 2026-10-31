using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Fitness;
using RunningDashboard.Api.Lists;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Records;
using RunningDashboard.Api.Strava;
using RunningDashboard.Api.Training;
using RunningDashboard.Api.Weather;

namespace RunningDashboard.Api.Runs;

public static class RunEndpoints
{
    /// <summary>What the Runs list sorts by, besides date.</summary>
    private static readonly Dictionary<string, Func<RunExportResponse, double?>> Figures = new()
    {
        ["distance"] = r => r.Distance,
        ["movingTime"] = r => r.MovingTime,
        ["pace"] = r => r.Pace,
    };

    public static void MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        // With a shoe (Strava's gear id), only the Runs logged with that Shoe.
        app.MapGet("/api/runs", async ([AsParameters] ListQuery query, string? shoe, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = query.Validate(Figures.Keys, "Runs");
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var runs = await MatchingAsync(query, shoe, db, cancellationToken);
            var page = query.PageOf(runs, r => r.StartLocal, Figures).Select(RunResponse.From).ToList();
            return Results.Ok(new RunPageResponse(page, query.PageOrDefault, query.PageSizeOrDefault, runs.Count));
        });

        // Every Run the list would show, on one "page", with what a spreadsheet wants besides; the page and its size
        // are ignored.
        app.MapGet("/api/runs/export", async ([AsParameters] ListQuery query, string? shoe, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = query.Validate(Figures.Keys, "Runs");
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            return Results.Ok(query.Sorted(await MatchingAsync(query, shoe, db, cancellationToken), r => r.StartLocal, Figures).ToList());
        });

        // One year's Runs, road, trail and treadmill alike, added up per calendar month, by local start; all twelve
        // months, empty ones as zero. Also per day of the week, Monday to Sunday.
        app.MapGet("/api/runs/months", async (int? year, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            if (YearParameter.Reject(year) is { } rejected)
                return rejected;

            var selectedYear = year ?? clock.GetLocalNow().Year;
            var from = new DateTime(selectedYear, 1, 1);
            var until = from.AddYears(1);
            var runs = await db.Runs
                .Where(r => r.StartLocal >= from && r.StartLocal < until)
                .Select(r => new { r.StartLocal, r.Distance, r.MovingTime })
                .ToListAsync(cancellationToken);
            var byMonth = runs.ToLookup(r => r.StartLocal.Month);
            var months = Enumerable.Range(1, 12)
                .Select(month =>
                {
                    var inMonth = byMonth[month].ToList();
                    var distance = inMonth.Sum(r => r.Distance);
                    var movingTime = inMonth.Sum(r => r.MovingTime);
                    return new MonthTotalsResponse(month, inMonth.Count, distance, movingTime, Run.PaceOf(movingTime, distance));
                })
                .ToList();
            var byWeekday = runs.ToLookup(r => r.StartLocal.DayOfWeek);
            var weekdays = Enumerable.Range(1, 7)
                // Monday to Saturday, then Sunday, as Training Weeks run.
                .Select(i => (DayOfWeek)(i % 7))
                .Select(day => new WeekdayTotalsResponse(ApiNames.Of(day), byWeekday[day].Count(), byWeekday[day].Sum(r => r.Distance)))
                .ToList();
            var firstRun = await db.Runs.MinAsync(r => (DateTime?)r.StartLocal, cancellationToken);
            return Results.Ok(new RunMonthsResponse(selectedYear, firstRun?.Year, months, weekdays));
        });

        app.MapGet("/api/runs/{id:long}", async (long id, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var run = await db.Runs
                .Include(r => r.BestEfforts)
                .Include(r => r.Splits)
                // Joined in one query, each Best Effort times each Split repeats the Run with its raw JSON.
                .AsSplitQuery()
                .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (run is null)
                return Results.NotFound();

            var shoe = run.ShoeId is null ? null : await db.Shoes.FindAsync([run.ShoeId], cancellationToken);
            var personalBestDistances = await PersonalBests.DistancesSetByAsync(run, db, cancellationToken);
            return Results.Ok(RunDetailResponse.From(run, shoe?.Name, personalBestDistances, await FitnessOfAsync(run, db, cancellationToken)));
        });

        // Apart from the Run, so its page need not wait for Open-Meteo.
        app.MapGet("/api/runs/{id:long}/temperature", async (long id, AppDbContext db, WeatherClient weather, CancellationToken cancellationToken) =>
        {
            var run = await db.Runs.FindAsync([id], cancellationToken);
            if (run is null)
                return Results.NotFound();

            await LookUpTemperatureAsync(run, db, weather, cancellationToken);
            return Results.Ok(new RunTemperatureResponse(run.Temperature));
        });
    }

    /// <summary>
    /// The Runs matching the search and date range (with a shoe, only those logged with it), unsorted, with all the
    /// export has of each; the list shows less. Filtered in memory over a lean projection: a few thousand Runs at most,
    /// and it keeps the name search case-insensitive beyond ASCII (SQLite's LIKE is not), independent of the server's
    /// culture.
    /// </summary>
    private static async Task<List<RunExportResponse>> MatchingAsync(ListQuery query, string? shoe, AppDbContext db, CancellationToken cancellationToken)
    {
        var shoeNames = await db.GetShoeNamesAsync(cancellationToken);
        return [.. (await db.Runs
                .Where(r => shoe == null || r.ShoeId == shoe)
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
                StravaLinks.Activity(r.Id)))];
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
        if (!FitnessFigures.IsFitnessRun(run.IsTreadmill, run.StartLocal, await db.GetRaceDaysAsync(cancellationToken)))
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
