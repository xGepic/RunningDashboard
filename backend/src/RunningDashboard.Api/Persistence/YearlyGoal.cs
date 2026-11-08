namespace RunningDashboard.Api.Persistence;

/// <summary>
/// The Yearly Goal: the distance the Athlete wants to run in a calendar year. Only the current year's counts; earlier
/// years' are kept, one per year.
/// </summary>
public sealed class YearlyGoal
{
    /// <summary>The key: one goal per year.</summary>
    public int Year { get; set; }

    /// <summary>Metres.</summary>
    public double Distance { get; set; }
}
