namespace RunningDashboard.Api.Persistence;

/// <summary>
/// The sports the dashboard keeps, by Strava sport type: running, and Rides and Swims, which are only listed. Every
/// other Activity is ignored.
/// </summary>
public static class Sports
{
    /// <summary>Road, trail and virtual.</summary>
    public static readonly string[] Runs = ["Run", "TrailRun", VirtualRun];

    /// <summary>A Run in an app such as Zwift; like one on a treadmill, its Pace is unreliable.</summary>
    public const string VirtualRun = "VirtualRun";

    /// <summary>Road, virtual, gravel and mountain bike; e-bike rides are not Rides.</summary>
    public static readonly string[] Rides = ["Ride", VirtualRide, "GravelRide", "MountainBikeRide"];

    /// <summary>A Ride on a trainer in an app such as Zwift; the other Rides are outdoor ones.</summary>
    public const string VirtualRide = "VirtualRide";

    /// <summary>In a pool or open water.</summary>
    public static readonly string[] Swims = ["Swim"];

    public static bool IsRun(string sportType) => Runs.Contains(sportType);

    public static bool IsListed(string sportType) => Rides.Contains(sportType) || Swims.Contains(sportType);
}
