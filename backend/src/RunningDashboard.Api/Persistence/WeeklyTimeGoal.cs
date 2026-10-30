namespace RunningDashboard.Api.Persistence;

/// <summary>
/// The Weekly Time Goal: the Training Time the Athlete wants in every Training Week, Runs, Rides and Swims together.
/// There is at most one row, for every week alike.
/// </summary>
public sealed class WeeklyTimeGoal
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>Seconds.</summary>
    public int Time { get; set; }
}
