using System.Globalization;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Training;

/// <summary>
/// Rules Goal Races and Training Plans have to follow.
/// </summary>
public static class PlanningRules
{
    /// <summary>
    /// Why <paramref name="block"/> cannot be a Training Block next to <paramref name="otherRaces"/>, if it cannot:
    /// blocks must not overlap, so "the current Training Block" is never ambiguous.
    /// </summary>
    public static string? OverlapError(TrainingBlock block, IEnumerable<GoalRace> otherRaces) =>
        otherRaces.FirstOrDefault(other => other.TrainingBlock.Overlaps(block)) is { } overlapping
            ? $"The Training Block ({block}) overlaps the one of {overlapping.Name} ({overlapping.TrainingBlock})."
            : null;

    /// <summary>
    /// Why a Goal Race on <paramref name="date"/> cannot be added next to <paramref name="otherRaces"/>, if it cannot:
    /// only one Goal Race is upcoming (race day today or later) at a time, so there is only ever one active Training
    /// Plan. Past Goal Races are not limited.
    /// </summary>
    public static string? UpcomingRaceError(DateOnly date, IEnumerable<GoalRace> otherRaces, DateOnly today) =>
        date >= today && otherRaces.Where(r => r.Date >= today).MinBy(r => r.Date) is { } upcoming
            ? $"{upcoming.Name} on {upcoming.Date.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} is still to come, and only one Goal Race can be upcoming at a time."
            : null;

    /// <summary>
    /// What is wrong with a Training Plan's shape, keyed "length" or "phases": it is 12, 16 or 20 weeks long, made of
    /// 4-week Phases, and the last Phase is a Taper. Empty if nothing is.
    /// </summary>
    public static Dictionary<string, string[]> TrainingPlanErrors(int? length, IReadOnlyList<PhaseType>? phases)
    {
        var lengths = TrainingPlan.Lengths;
        if (length is not { } weeks || !lengths.Contains(weeks))
            return new() { ["length"] = [$"A Training Plan is {string.Join(", ", lengths.SkipLast(1))} or {lengths[^1]} weeks long."] };
        if (phases is null || phases.Count != weeks / TrainingPlan.PhaseWeeks)
            return new() { ["phases"] = [$"A {weeks}-week plan has {weeks / TrainingPlan.PhaseWeeks} Phases of {TrainingPlan.PhaseWeeks} weeks."] };
        if (phases.Any(p => !Enum.IsDefined(p)))
            return new() { ["phases"] = ["Each Phase is a Build, Peak or Taper."] };
        if (phases[^1] != PhaseType.Taper)
            return new() { ["phases"] = ["The last Phase must be a Taper."] };
        return [];
    }

    /// <summary>Why the week <paramref name="weeksToRace"/> cannot be a Recovery Week, if it cannot: the Race Week stays its own thing.</summary>
    public static string? RecoveryWeekError(int weeksToRace, bool recoveryWeek) =>
        recoveryWeek && weeksToRace == 0 ? "The Race Week can't be a Recovery Week." : null;
}
