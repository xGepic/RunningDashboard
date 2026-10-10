using RunningDashboard.Api.Training;

namespace RunningDashboard.Api.Persistence;

/// <summary>
/// A race the Athlete trains for. Its Training Block ends with the Race Week.
/// </summary>
public sealed class GoalRace
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>Metres.</summary>
    public double Distance { get; set; }

    /// <summary>Seconds; the finish time the Athlete wants to beat, if they set one.</summary>
    public int? TargetTime { get; set; }

    /// <summary>Length of the Training Block in Training Weeks, including the Race Week.</summary>
    public int TrainingBlockWeeks { get; set; }

    public TrainingBlock TrainingBlock => TrainingBlock.Ending(Date, TrainingBlockWeeks);
}
