using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Persistence;

/// <summary>
/// A Strava activity with a cycling sport type. Rides are only listed; they never count toward the running figures.
/// </summary>
public sealed class Ride
{
    private static readonly HashSet<string> CyclingSportTypes = ["Ride", "VirtualRide", "GravelRide", "MountainBikeRide"];

    /// <summary>Strava's activity id.</summary>
    public long Id { get; set; }

    public required string Name { get; set; }

    public required string SportType { get; set; }

    public DateTime StartUtc { get; set; }

    /// <summary>Wall-clock start time where the Ride happened.</summary>
    public DateTime StartLocal { get; set; }

    /// <summary>Metres.</summary>
    public double Distance { get; set; }

    /// <summary>Seconds.</summary>
    public int MovingTime { get; set; }

    /// <summary>Seconds.</summary>
    public int ElapsedTime { get; set; }

    /// <summary>Metres.</summary>
    public double ElevationGain { get; set; }

    public required string RawSummaryJson { get; set; }

    public static bool IsCyclingSportType(string sportType) => CyclingSportTypes.Contains(sportType);

    /// <summary>
    /// Avg Speed: distance per Moving Time, in kilometres per hour. A Ride without Moving Time has none.
    /// </summary>
    public static double? SpeedOf(int movingTime, double distance) => movingTime > 0 ? distance / 1000 / (movingTime / 3600.0) : null;

    public static Ride From(StravaActivity activity)
    {
        var ride = new Ride { Id = activity.Id, Name = activity.Name, SportType = activity.SportType, RawSummaryJson = activity.RawJson };
        ride.UpdateFrom(activity);
        return ride;
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
        RawSummaryJson = activity.RawJson;
    }
}
