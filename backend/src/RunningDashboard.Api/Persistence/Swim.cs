using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Persistence;

/// <summary>
/// A Strava activity with the swimming sport type, in a pool or open water. Swims are only listed; they never count
/// toward the running figures.
/// </summary>
public sealed class Swim
{
    private const string SwimmingSportType = "Swim";

    /// <summary>Strava's activity id.</summary>
    public long Id { get; set; }

    public required string Name { get; set; }

    public required string SportType { get; set; }

    public DateTime StartUtc { get; set; }

    /// <summary>Wall-clock start time where the Swim happened.</summary>
    public DateTime StartLocal { get; set; }

    /// <summary>Metres.</summary>
    public double Distance { get; set; }

    /// <summary>Seconds.</summary>
    public int MovingTime { get; set; }

    /// <summary>Seconds.</summary>
    public int ElapsedTime { get; set; }

    public required string RawSummaryJson { get; set; }

    public static bool IsSwimmingSportType(string sportType) => sportType == SwimmingSportType;

    /// <summary>
    /// Swim Pace: Moving Time per 100 metres, in seconds. A Swim without distance has none.
    /// </summary>
    public static double? PaceOf(int movingTime, double distance) => distance > 0 ? movingTime / (distance / 100) : null;

    public static Swim From(StravaActivity activity)
    {
        var swim = new Swim { Id = activity.Id, Name = activity.Name, SportType = activity.SportType, RawSummaryJson = activity.RawJson };
        swim.UpdateFrom(activity);
        return swim;
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
        RawSummaryJson = activity.RawJson;
    }
}
