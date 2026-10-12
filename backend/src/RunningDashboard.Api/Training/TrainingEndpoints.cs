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
    /// <param name="Phases">The Training Plan's Phases, first to last; none without a plan.</param>
    /// <param name="Weeks">First week to Race Week.</param>
    /// <param name="Comparison">The Training Block this one is compared with; none when the comparison is off.</param>
    private sealed record BlockResponse(
        GoalRaceResponse GoalRace,
        int? TargetPace,
        IReadOnlyList<PhaseResponse>? Phases,
        IReadOnlyList<WeekResponse> Weeks,
        ComparisonResponse? Comparison);

    /// <param name="Type">"build", "peak" or "taper".</param>
    private sealed record PhaseResponse(string Type, int FirstWeeksToRace, int LastWeeksToRace)
    {
        public static List<PhaseResponse>? Of(TrainingPlan? plan) =>
            plan?.PhaseSpans()
                .Select(p => new PhaseResponse(ApiNames.Of(p.Type), p.FirstWeeksToRace, p.LastWeeksToRace))
                .ToList();
    }

    /// <summary>Another Goal Race's Training Block, lined up with the viewed one by Weeks to Race.</summary>
    /// <param name="Weeks">
    /// One per Weeks to Race in either block, first to Race Week. Both sides' Long Runs are compared with the viewed
    /// Goal Race's Target Pace: the Pace needed now.
    /// </param>
    private sealed record ComparisonResponse(GoalRaceResponse GoalRace, IReadOnlyList<LinedUpWeekResponse> Weeks);

    /// <param name="Viewed">The viewed block's week; none if that block does not have it.</param>
    /// <param name="Compared">The compared block's week; none if that block does not have it.</param>
    private sealed record LinedUpWeekResponse(int WeeksToRace, WeekResponse? Viewed, WeekResponse? Compared)
    {
        public static LinedUpWeekResponse From(LinedUpWeek week, int? targetPace) => new(
            week.WeeksToRace,
            week.Viewed is null ? null : WeekResponse.From(week.Viewed, targetPace),
            week.Compared is null ? null : WeekResponse.From(week.Compared, targetPace));
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
    /// <param name="Plan">What the Training Plan has for the week, against what was run; none without a plan.</param>
    private sealed record WeekResponse(int WeeksToRace, DateOnly Start, DateOnly End, string Status, ActualResponse? Actual, PlanResponse? Plan)
    {
        public static WeekResponse From(TrainingBlockWeek week, int? targetPace) => new(
            week.WeeksToRace,
            week.Week.Start,
            week.Week.End,
            ApiNames.Of(week.Status),
            week.Actual is { } actual
                ? new ActualResponse(actual.Runs, actual.Distance, actual.MovingTime, actual.RelativeEffort, LongRunResponse.From(actual.LongRun, targetPace))
                : null,
            week.Plan is { } plan
                ? new PlanResponse(
                    ApiNames.Of(plan.Phase),
                    plan.RecoveryWeek,
                    plan.PlannedDistance,
                    plan.PlannedLongRun,
                    plan.DistanceMet,
                    plan.LongRunMet,
                    [.. plan.Days.Select(d => new DayResponse(d.Date, ApiNames.Of(d.State), d.RunType is { } runType ? ApiNames.Of(runType) : null))])
                : null);
    }

    /// <param name="Phase">"build", "peak" or "taper".</param>
    /// <param name="PlannedDistance">Metres; none if not planned.</param>
    /// <param name="PlannedLongRun">Metres; none if not planned.</param>
    /// <param name="DistanceMet">
    /// True once the week's distance reached the plan's, false once the week is over without; none while it still runs
    /// short, for a future week, or with nothing planned.
    /// </param>
    /// <param name="LongRunMet">Likewise for the Long Run.</param>
    /// <param name="Days">Monday to Sunday: the calendar strip.</param>
    private sealed record PlanResponse(
        string Phase,
        bool RecoveryWeek,
        double? PlannedDistance,
        double? PlannedLongRun,
        bool? DistanceMet,
        bool? LongRunMet,
        IReadOnlyList<DayResponse> Days);

    /// <param name="State">
    /// "ran" (a Running Day with a Run), "missed" (a past Running Day without one), "unplanned" (a Run on another day),
    /// "planned" (a Running Day still to come) or "rest".
    /// </param>
    /// <param name="RunType">
    /// The Running Day's: "easy", "long", "tempo", "intervals" or "race"; none for a rest day or a Run on an unplanned day.
    /// </param>
    private sealed record DayResponse(DateOnly Date, string State, string? RunType);

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
