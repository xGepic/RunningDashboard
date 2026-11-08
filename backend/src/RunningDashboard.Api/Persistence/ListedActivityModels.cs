namespace RunningDashboard.Api.Persistence;

/// <summary>A Listed Activity's figures from its stored summary.</summary>
/// <param name="AverageHeartRate">Beats per minute; none without a heart-rate recording.</param>
/// <param name="MaxHeartRate">Beats per minute; none without a heart-rate recording.</param>
/// <param name="MaxSpeed">Kilometres per hour; none if Strava sent none.</param>
/// <param name="AveragePower">Watts: measured with a power meter, or else Strava's estimate for a Ride; none for most Swims.</param>
/// <param name="AverageCadence">Pedal revolutions per minute for a Ride; none without a cadence sensor.</param>
/// <param name="Route">
/// The simplified route, as Strava encodes it (Google's encoded polyline). None for an Activity without GPS, such as a
/// Swim in a pool.
/// </param>
public sealed record ListedActivitySummary(
    double? AverageHeartRate,
    double? MaxHeartRate,
    double? MaxSpeed,
    double? AveragePower,
    double? AverageCadence,
    string? Route);
