using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Training;

/// <summary>What the Training page needs to know about one Run.</summary>
/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
public sealed record RunSummary(long Id, string Name, DateTime StartLocal, double Distance, int MovingTime, double? RelativeEffort);

public enum WeekStatus
{
    Past,

    /// <summary>The week today falls in; its actual values are those so far.</summary>
    Current,

    /// <summary>Not started yet, so it has no actual values.</summary>
    Future,
}

/// <summary>One Training Week of a Training Block with what was actually run in it.</summary>
/// <param name="Actual">None for a future week.</param>
/// <param name="Plan">What the Training Plan has for the week, against what was run; none without a plan.</param>
public sealed record TrainingBlockWeek(int WeeksToRace, TrainingWeek Week, WeekStatus Status, WeekActual? Actual, WeekPlan? Plan);

/// <summary>How one day of a planned Training Week went.</summary>
public enum DayState
{
    /// <summary>A Running Day with a Run.</summary>
    Ran,

    /// <summary>A Running Day that has passed without a Run.</summary>
    Missed,

    /// <summary>A Run on a day that is not a Running Day.</summary>
    Unplanned,

    /// <summary>A Running Day still to come, today included.</summary>
    Planned,

    /// <summary>Neither a Running Day nor a Run.</summary>
    Rest,
}

/// <summary>One day of a planned Training Week on the calendar strip.</summary>
public sealed record PlanDay(DateOnly Date, DayState State);

/// <summary>A planned Training Week against what was run in it.</summary>
/// <param name="PlannedDistance">Metres; none if not planned.</param>
/// <param name="PlannedLongRun">Metres; none if not planned.</param>
/// <param name="DistanceMet">
/// True once the week's distance reached the planned one, in km to two decimals as the Athlete reads them; false
/// once a week is over without; none while it still runs short, for a future week, or without a planned distance.
/// </param>
/// <param name="LongRunMet">Likewise for the Long Run.</param>
/// <param name="Days">Monday to Sunday.</param>
public sealed record WeekPlan(
    PhaseType Phase,
    bool RecoveryWeek,
    double? PlannedDistance,
    double? PlannedLongRun,
    bool? DistanceMet,
    bool? LongRunMet,
    IReadOnlyList<PlanDay> Days);

/// <summary>One Weeks to Race across two Training Blocks; either block may not have that week.</summary>
public sealed record LinedUpWeek(int WeeksToRace, TrainingBlockWeek? Viewed, TrainingBlockWeek? Compared);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="RelativeEffort">Sum over the week's Runs that have one; none if no Run has.</param>
/// <param name="LongRun">The longest Run of the week; none in a week without Runs.</param>
public sealed record WeekActual(int Runs, double Distance, int MovingTime, double? RelativeEffort, RunSummary? LongRun);
