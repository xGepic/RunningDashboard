namespace RunningDashboard.Api.Persistence;

/// <summary>
/// One kilometre, or one mile, of a Run as Strava splits it; the last split is usually shorter.
/// </summary>
public sealed class Split
{
    public long RunId { get; set; }

    public SplitUnit Unit { get; set; }

    /// <summary>1-based position within the Run, among the splits of its unit.</summary>
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

/// <summary>What a Split is one of: Strava splits every Run both ways.</summary>
public enum SplitUnit
{
    Kilometre,
    Mile,
}
