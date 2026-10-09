namespace RunningDashboard.Api.Persistence;

/// <summary>
/// One kilometre of a Run as Strava splits it; the last split is usually shorter.
/// </summary>
public sealed class Split
{
    public long RunId { get; set; }

    /// <summary>1-based position within the Run.</summary>
    public int Index { get; set; }

    /// <summary>Metres.</summary>
    public double Distance { get; set; }

    /// <summary>Seconds.</summary>
    public int MovingTime { get; set; }

    /// <summary>Seconds.</summary>
    public int ElapsedTime { get; set; }

    /// <summary>Metres, negative when the split went downhill overall.</summary>
    public double ElevationDifference { get; set; }
}
