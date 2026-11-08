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

    /// <summary>Average beats per minute; none without a heart-rate recording.</summary>
    public double? HeartRate { get; set; }

    /// <summary>Metres per second, as Strava reckons the split would have been run on the flat.</summary>
    public double? GradeAdjustedSpeed { get; set; }
}
