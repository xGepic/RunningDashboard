namespace RunningDashboard.Api.Persistence;

/// <summary>
/// The Yearly Goal: the distance the Athlete wants to run in a calendar year. There is at most one row; once its year
/// is over, it no longer counts.
/// </summary>
public sealed class YearlyGoal
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public int Year { get; set; }

    /// <summary>Metres.</summary>
    public double Distance { get; set; }
}
