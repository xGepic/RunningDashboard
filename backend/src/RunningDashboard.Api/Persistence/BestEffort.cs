namespace RunningDashboard.Api.Persistence;

/// <summary>
/// Strava's fastest time over a standard distance within one Run, including parts of longer Runs.
/// </summary>
public sealed class BestEffort
{
    public int Id { get; set; }

    public long RunId { get; set; }

    /// <summary>Strava's name for the distance, e.g. "5K" or "Half-Marathon".</summary>
    public required string Name { get; set; }

    /// <summary>Metres.</summary>
    public double Distance { get; set; }

    /// <summary>Seconds.</summary>
    public int ElapsedTime { get; set; }
}
