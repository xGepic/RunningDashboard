using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.GoalRaces;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Records;

namespace RunningDashboard.Api.Review;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        // One calendar year's highlights, the current year's so far.
        app.MapGet("/api/review", async (int? year, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            if (year is < 1900 or > 2099)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["year"] = ["The year is between 1900 and 2099."] });

            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var selectedYear = year ?? today.Year;
            var yearStart = new DateTime(selectedYear, 1, 1);
            var nextYearStart = yearStart.AddYears(1);

            // This year's Runs and last year's, to compare with.
            var previousYearStart = yearStart.AddYears(-1);
            var runs = await db.Runs
                .Where(r => r.StartLocal >= previousYearStart && r.StartLocal < nextYearStart)
                .Select(r => new ReviewRun(r.Id, r.Name, r.StartLocal, r.Distance, r.MovingTime, r.ElevationGain, r.ShoeId))
                .ToListAsync(cancellationToken);
            var firstRun = await db.Runs.MinAsync(r => (DateTime?)r.StartLocal, cancellationToken);

            // Every Best Effort up to the year's end: the Personal Bests before the year decide which of its own were ones.
            var efforts = (await PersonalBests.BestEfforts(db).ToListAsync(cancellationToken)).Where(e => e.StartLocal < nextYearStart).ToList();

            var firstDay = DateOnly.FromDateTime(yearStart);
            var lastDay = DateOnly.FromDateTime(nextYearStart).AddDays(-1);
            var races = await db.GoalRaces
                .Where(r => r.Date >= firstDay && r.Date <= lastDay)
                .OrderBy(r => r.Date)
                .ToListAsync(cancellationToken);
            var results = await RaceResults.OfAsync(races, db, cancellationToken);

            var listed = await db.ListedActivities
                .Where(a => a.StartLocal >= yearStart && a.StartLocal < nextYearStart)
                .Select(a => a.SportType)
                .ToListAsync(cancellationToken);
            var shoeNames = await db.Shoes.ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

            return Results.Ok(YearReviewResponse.Of(
                selectedYear,
                today,
                firstRun,
                runs,
                efforts,
                [.. races.Select(r => ReviewGoalRaceResponse.From(r, results.GetValueOrDefault(r.Id)))],
                shoeNames,
                rides: listed.Count(Sports.Rides.Contains),
                swims: listed.Count(Sports.Swims.Contains)));
        });
    }
}
