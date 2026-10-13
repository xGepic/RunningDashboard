using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Training;

/// <param name="View">
/// "block": a Goal Race's Training Block; "countdown": today is in no block, but a Goal Race is ahead; "none":
/// neither.
/// </param>
internal sealed record TrainingResponse(string View, BlockResponse? Block, CountdownResponse? Countdown);

/// <param name="GoalRace">The Goal Race whose Training Block comes next.</param>
/// <param name="TrainingBlockStart">The Monday that block starts.</param>
/// <param name="DaysRemaining">Days from today to that Monday.</param>
/// <param name="RecentWeeks">The last Training Weeks up to the current one, oldest first; weeks without Runs as zero.</param>
internal sealed record CountdownResponse(GoalRaceResponse GoalRace, DateOnly TrainingBlockStart, int DaysRemaining, IReadOnlyList<RecentWeekResponse> RecentWeeks);

/// <param name="Start">The Monday.</param>
/// <param name="Distance">Metres.</param>
internal sealed record RecentWeekResponse(DateOnly Start, double Distance);

/// <param name="TargetPace">
/// Target Time ÷ Goal Race distance, in whole seconds per km rounded down; none without a Target Time.
/// </param>
/// <param name="Phases">The Training Plan's Phases, first to last; none without a plan.</param>
/// <param name="Weeks">First week to Race Week.</param>
/// <param name="Comparison">The Training Block this one is compared with; none when the comparison is off.</param>
internal sealed record BlockResponse(
    GoalRaceResponse GoalRace,
    int? TargetPace,
    IReadOnlyList<PhaseResponse>? Phases,
    IReadOnlyList<WeekResponse> Weeks,
    ComparisonResponse? Comparison);

/// <param name="Type">"build", "peak" or "taper".</param>
internal sealed record PhaseResponse(string Type, int FirstWeeksToRace, int LastWeeksToRace)
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
internal sealed record ComparisonResponse(GoalRaceResponse GoalRace, IReadOnlyList<LinedUpWeekResponse> Weeks);

/// <param name="Viewed">The viewed block's week; none if that block does not have it.</param>
/// <param name="Compared">The compared block's week; none if that block does not have it.</param>
internal sealed record LinedUpWeekResponse(int WeeksToRace, WeekResponse? Viewed, WeekResponse? Compared)
{
    public static LinedUpWeekResponse From(LinedUpWeek week, int? targetPace) => new(
        week.WeeksToRace,
        week.Viewed is null ? null : WeekResponse.From(week.Viewed, targetPace),
        week.Compared is null ? null : WeekResponse.From(week.Compared, targetPace));
}

/// <param name="Distance">Metres.</param>
/// <param name="TargetTime">Seconds.</param>
internal sealed record GoalRaceResponse(int Id, string Name, DateOnly Date, double Distance, int? TargetTime)
{
    public static GoalRaceResponse From(GoalRace race) => new(race.Id, race.Name, race.Date, race.Distance, race.TargetTime);
}

/// <param name="Start">The Monday.</param>
/// <param name="End">The Sunday.</param>
/// <param name="Status">"past", "current" (today is in it) or "future".</param>
/// <param name="Actual">What was run; none for a future week.</param>
/// <param name="Plan">What the Training Plan has for the week, against what was run; none without a plan.</param>
internal sealed record WeekResponse(int WeeksToRace, DateOnly Start, DateOnly End, string Status, ActualResponse? Actual, PlanResponse? Plan)
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
                [.. plan.Days.Select(d => new DayResponse(d.Date, ApiNames.Of(d.State)))])
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
internal sealed record PlanResponse(
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
internal sealed record DayResponse(DateOnly Date, string State);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="RelativeEffort">Sum over the week's Runs; none if no Run has one.</param>
internal sealed record ActualResponse(int Runs, double Distance, int MovingTime, double? RelativeEffort, LongRunResponse? LongRun);

/// <param name="Distance">Metres.</param>
/// <param name="Pace">Seconds per km.</param>
/// <param name="PaceVsTarget">
/// Whole seconds per km slower (positive) or faster (negative) than the Target Pace; none without a Target Time.
/// </param>
internal sealed record LongRunResponse(long RunId, string Name, DateTime StartLocal, double Distance, double? Pace, int? PaceVsTarget)
{
    public static LongRunResponse? From(RunSummary? run, int? targetPace)
    {
        if (run is null)
            return null;
        var pace = Run.PaceOf(run.MovingTime, run.Distance);
        return new LongRunResponse(run.Id, run.Name, run.StartLocal, run.Distance, pace, TrainingCalendar.PaceVsTarget(pace, targetPace));
    }
}
