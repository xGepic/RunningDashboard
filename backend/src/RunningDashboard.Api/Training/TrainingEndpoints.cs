using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Training;

public static class TrainingEndpoints
{
    public static void MapTrainingEndpoints(this IEndpointRouteBuilder app)
    {
        // The Training Block of the given Goal Race; without one, the block today falls in, or else a countdown to the
        // next one. A block is compared with that of compareWith (a Goal Race id, or "none"), by default with the
        // previous Goal Race's.
        app.MapGet("/api/training", async (
            int? goalRaceId,
            string? compareWith,
            AppDbContext db,
            TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var races = await db.GoalRaces.ToListAsync(cancellationToken);
            GoalRace? race;
            if (goalRaceId is not null)
            {
                race = races.SingleOrDefault(r => r.Id == goalRaceId);
                if (race is null)
                    return Results.NotFound();
            }
            else
            {
                race = TrainingCalendar.RaceTrainingFor(races, today);
                if (race is null)
                {
                    return TrainingCalendar.NextBlockRace(races, today) is { } next
                        ? Results.Ok(new TrainingResponse("countdown", null, await CountdownAsync(next, today, db, cancellationToken)))
                        : Results.Ok(new TrainingResponse("none", null, null));
                }
            }

            var (compared, rejected) = ComparedRace(compareWith, race, races);
            if (rejected is not null)
                return rejected;

            var weeks = await WeeksAsync(race, today, db, cancellationToken);
            var comparison = compared is null
                ? null
                : new ComparisonResponse(
                    GoalRaceResponse.From(compared),
                    [.. TrainingCalendar.LineUp(weeks, await WeeksAsync(compared, today, db, cancellationToken)).Select(w => LinedUpWeekResponse.From(w, race.TargetPace))]);
            return Results.Ok(new TrainingResponse(
                "block",
                new BlockResponse(
                    GoalRaceResponse.From(race),
                    race.TargetPace,
                    PhaseResponse.Of(race.TrainingPlan),
                    [.. weeks.Select(w => WeekResponse.From(w, race.TargetPace))],
                    comparison),
                null));
        });
    }

    /// <summary>
    /// The Goal Race whose block <paramref name="race"/>'s is compared with: <paramref name="compareWith"/> is absent
    /// for the previous Goal Race, "none" for no comparison, or another Goal Race's id; otherwise the request is rejected.
    /// </summary>
    private static (GoalRace? Compared, IResult? Rejected) ComparedRace(string? compareWith, GoalRace race, IReadOnlyCollection<GoalRace> races)
    {
        if (compareWith is null)
            return (TrainingCalendar.PreviousRace(races, race), null);
        if (compareWith == "none")
            return (null, null);
        if (!int.TryParse(compareWith, out var id) || id == race.Id)
        {
            return (null, Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(compareWith)] = ["Compare with another Goal Race's id, or none."],
            }));
        }

        return races.SingleOrDefault(r => r.Id == id) is { } compared ? (compared, null) : (null, Results.NotFound());
    }

    /// <summary>The weeks of <paramref name="race"/>'s Training Block with what was run.</summary>
    private static async Task<List<TrainingBlockWeek>> WeeksAsync(GoalRace race, DateOnly today, AppDbContext db, CancellationToken cancellationToken)
    {
        var block = race.TrainingBlock;
        var runs = await RunsAsync(block.Start, block.End, db, cancellationToken);
        return TrainingCalendar.WeeksOf(block, runs, today, race.TrainingPlan);
    }

    private static async Task<CountdownResponse> CountdownAsync(GoalRace next, DateOnly today, AppDbContext db, CancellationToken cancellationToken)
    {
        var firstWeek = TrainingCalendar.FirstRecentWeek(today);
        var runs = await RunsAsync(firstWeek.Start, today, db, cancellationToken);
        return new CountdownResponse(
            GoalRaceResponse.From(next),
            next.TrainingBlock.Start,
            TrainingCalendar.DaysUntilBlock(next, today),
            [.. TrainingCalendar.DistanceByWeek(firstWeek, TrainingWeek.Of(today), runs).Select(w => new RecentWeekResponse(w.Week.Start, w.Distance))]);
    }

    /// <summary>The Runs that started (local time) from <paramref name="firstDay"/> through <paramref name="lastDay"/>.</summary>
    private static Task<List<RunSummary>> RunsAsync(DateOnly firstDay, DateOnly lastDay, AppDbContext db, CancellationToken cancellationToken)
    {
        var from = firstDay.ToDateTime(TimeOnly.MinValue);
        var until = lastDay.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return db.Runs
            .Where(r => r.StartLocal >= from && r.StartLocal < until)
            .Select(r => new RunSummary(r.Id, r.Name, r.StartLocal, r.Distance, r.MovingTime, r.RelativeEffort))
            .ToListAsync(cancellationToken);
    }
}
