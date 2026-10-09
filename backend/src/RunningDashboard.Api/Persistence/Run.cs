using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Persistence;

/// <summary>
/// A Strava activity with a running sport type. Other Activities are never stored.
/// </summary>
public sealed class Run
{
    private static readonly HashSet<string> RunningSportTypes = ["Run", "TrailRun", "VirtualRun"];

    /// <summary>Strava's activity id.</summary>
    public long Id { get; set; }

    public required string Name { get; set; }

    public required string SportType { get; set; }

    public DateTime StartUtc { get; set; }

    /// <summary>Wall-clock start time where the Run happened; decides its Training Week.</summary>
    public DateTime StartLocal { get; set; }

    /// <summary>Metres.</summary>
    public double Distance { get; set; }

    /// <summary>Seconds.</summary>
    public int MovingTime { get; set; }

    /// <summary>Seconds.</summary>
    public int ElapsedTime { get; set; }

    /// <summary>Metres.</summary>
    public double ElevationGain { get; set; }

    public double? RelativeEffort { get; set; }

    /// <summary>Strava's gear id of the Shoe, if one was logged.</summary>
    public string? ShoeId { get; set; }

    public required string RawSummaryJson { get; set; }

    public static bool IsRunningSportType(string sportType) => RunningSportTypes.Contains(sportType);

    /// <summary>
    /// Pace: Moving Time per kilometre, in seconds. A Run without distance has none.
    /// </summary>
    public static double? PaceOf(int movingTime, double distance) => distance > 0 ? movingTime / (distance / 1000) : null;

    public static Run From(StravaActivity activity)
    {
        var run = new Run { Id = activity.Id, Name = activity.Name, SportType = activity.SportType, RawSummaryJson = activity.RawJson };
        run.UpdateFrom(activity);
        return run;
    }

    public void UpdateFrom(StravaActivity activity)
    {
        Name = activity.Name;
        SportType = activity.SportType;
        StartUtc = activity.StartUtc;
        StartLocal = activity.StartLocal;
        Distance = activity.Distance;
        MovingTime = activity.MovingTime;
        ElapsedTime = activity.ElapsedTime;
        ElevationGain = activity.ElevationGain;
        RelativeEffort = activity.RelativeEffort;
        ShoeId = activity.GearId;
        RawSummaryJson = activity.RawJson;
    }
}
