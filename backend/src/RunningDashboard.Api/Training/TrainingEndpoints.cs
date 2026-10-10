using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Training;

public static class TrainingEndpoints
{
    public static void MapTrainingEndpoints(this IEndpointRouteBuilder app)
    {
        // The Training Block of the given Goal Race; without one, the block today falls in, or else a countdown to the
        // next one.
        app.MapGet("/api/training", async (int? goalRaceId, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            GoalRace? race;
            if (goalRaceId is not null)
            {
                race = await db.GoalRaces.FindAsync([goalRaceId], cancellationToken);
                if (race is null)
                    return Results.NotFound();
            }
            else
            {
                var races = await db.GoalRaces.ToListAsync(cancellationToken);
                race = TrainingCalendar.RaceTrainingFor(races, today);
                if (race is null)
                {
                    return TrainingCalendar.NextBlockRace(races, today) is { } next
                        ? Results.Ok(new TrainingResponse("countdown", null, await CountdownAsync(next, today, db, cancellationToken)))
                        : Results.Ok(new TrainingResponse("none", null, null));
                }
            }

            var block = race.TrainingBlock;
            var runs = await RunsAsync(block.Start, block.End, db, cancellationToken);
            return Results.Ok(new TrainingResponse("block", BlockResponse.From(race, TrainingCalendar.WeeksOf(block, runs, today)), null));
        });
    }

    private static async Task<CountdownResponse> CountdownAsync(GoalRace next, DateOnly today, AppDbContext db, CancellationToken cancellationToken)
    {
        var firstWeek = TrainingCalendar.FirstRecentWeek(today);
        var runs = await RunsAsync(firstWeek.Start, today, db, cancellationToken);
        return new CountdownResponse(
            GoalRaceResponse.From(next),
            next.TrainingBlock.Start,
            TrainingCalendar.DaysUntilBlock(next, today),
            TrainingCalendar.DistanceByWeek(firstWeek, TrainingWeek.Of(today), runs)
                .Select(w => new RecentWeekResponse(w.Week.Start, w.Distance))
                .ToList());
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

    /// <param name="View">
    /// "block": a Goal Race's Training Block; "countdown": today is in no block, but a Goal Race is ahead; "none":
    /// neither.
    /// </param>
    private sealed record TrainingResponse(string View, BlockResponse? Block, CountdownResponse? Countdown);

    /// <param name="GoalRace">The Goal Race whose Training Block comes next.</param>
    /// <param name="TrainingBlockStart">The Monday that block starts.</param>
    /// <param name="DaysRemaining">Days from today to that Monday.</param>
    /// <param name="RecentWeeks">The last Training Weeks up to the current one, oldest first; weeks without Runs as zero.</param>
    private sealed record CountdownResponse(GoalRaceResponse GoalRace, DateOnly TrainingBlockStart, int DaysRemaining, IReadOnlyList<RecentWeekResponse> RecentWeeks);

    /// <param name="Start">The Monday.</param>
    /// <param name="Distance">Metres.</param>
    private sealed record RecentWeekResponse(DateOnly Start, double Distance);

    /// <param name="TargetPace">
    /// Target Time ÷ Goal Race distance, in whole seconds per km rounded down; none without a Target Time.
    /// </param>
    /// <param name="Weeks">First week to Race Week.</param>
    private sealed record BlockResponse(GoalRaceResponse GoalRace, int? TargetPace, IReadOnlyList<WeekResponse> Weeks)
    {
        public static BlockResponse From(GoalRace race, IEnumerable<TrainingBlockWeek> weeks) => new(
            GoalRaceResponse.From(race),
            race.TargetPace,
            weeks.Select(w => WeekResponse.From(w, race.TargetPace)).ToList());
    }

    /// <param name="Distance">Metres.</param>
    /// <param name="TargetTime">Seconds.</param>
    private sealed record GoalRaceResponse(int Id, string Name, DateOnly Date, double Distance, int? TargetTime)
    {
        public static GoalRaceResponse From(GoalRace race) => new(race.Id, race.Name, race.Date, race.Distance, race.TargetTime);
    }

    /// <param name="Start">The Monday.</param>
    /// <param name="End">The Sunday.</param>
    /// <param name="Status">"past", "current" (today is in it) or "future".</param>
    /// <param name="Actual">What was run; none for a future week.</param>
    private sealed record WeekResponse(int WeeksToRace, DateOnly Start, DateOnly End, string Status, ActualResponse? Actual)
    {
        public static WeekResponse From(TrainingBlockWeek week, int? targetPace) => new(
            week.WeeksToRace,
            week.Week.Start,
            week.Week.Start.AddDays(6),
            JsonNamingPolicy.CamelCase.ConvertName(week.Status.ToString()),
            week.Actual is { } actual
                ? new ActualResponse(actual.Runs, actual.Distance, actual.MovingTime, actual.RelativeEffort, LongRunResponse.From(actual.LongRun, targetPace))
                : null);
    }

    /// <param name="Distance">Metres.</param>
    /// <param name="MovingTime">Seconds.</param>
    /// <param name="RelativeEffort">Sum over the week's Runs; none if no Run has one.</param>
    private sealed record ActualResponse(int Runs, double Distance, int MovingTime, double? RelativeEffort, LongRunResponse? LongRun);

    /// <param name="Distance">Metres.</param>
    /// <param name="Pace">Seconds per km.</param>
    /// <param name="PaceVsTarget">
    /// Whole seconds per km slower (positive) or faster (negative) than the Target Pace; none without a Target Time.
    /// </param>
    private sealed record LongRunResponse(long RunId, string Name, DateTime StartLocal, double Distance, double? Pace, int? PaceVsTarget)
    {
        public static LongRunResponse? From(RunSummary? run, int? targetPace)
        {
            if (run is null)
                return null;
            var pace = Run.PaceOf(run.MovingTime, run.Distance);
            return new LongRunResponse(run.Id, run.Name, run.StartLocal, run.Distance, pace, TrainingCalendar.PaceVsTarget(pace, targetPace));
        }
    }
}
