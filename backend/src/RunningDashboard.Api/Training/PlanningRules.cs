using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Training;

/// <summary>
/// Rules a Goal Race has to follow next to the Athlete's other Goal Races.
/// </summary>
public static class PlanningRules
{
    /// <summary>
    /// The first of <paramref name="otherRaces"/> whose Training Block overlaps that of <paramref name="race"/>, if any.
    /// Blocks must not overlap, so "the current Training Block" is never ambiguous.
    /// </summary>
    public static GoalRace? OverlappingRace(GoalRace race, IEnumerable<GoalRace> otherRaces) =>
        otherRaces.FirstOrDefault(other => other.TrainingBlock.Overlaps(race.TrainingBlock));
}
