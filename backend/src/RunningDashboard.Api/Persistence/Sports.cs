namespace RunningDashboard.Api.Persistence;

/// <summary>
/// The sports listed besides running, by Strava sport type. Every other Activity that is not a Run is ignored.
/// </summary>
public static class Sports
{
    /// <summary>Road, virtual, gravel and mountain bike; e-bike rides are not Rides.</summary>
    public static readonly string[] Rides = ["Ride", VirtualRide, "GravelRide", "MountainBikeRide"];

    /// <summary>A Ride on a trainer in an app such as Zwift; the other Rides are outdoor ones.</summary>
    public const string VirtualRide = "VirtualRide";

    /// <summary>In a pool or open water.</summary>
    public static readonly string[] Swims = ["Swim"];

    public static bool IsListed(string sportType) => Rides.Contains(sportType) || Swims.Contains(sportType);
}
