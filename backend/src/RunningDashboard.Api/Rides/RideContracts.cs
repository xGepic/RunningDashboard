using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Rides;

/// <param name="Totals">Of every Ride that matches, not just this page's.</param>
/// <param name="Years">Every year with a Ride of any kind, newest first, whatever is asked for: what the list can be narrowed to.</param>
internal sealed record RidePageResponse(
    IReadOnlyList<RideResponse> Items, int Page, int PageSize, int TotalCount, RideTotalsResponse Totals, IReadOnlyList<int> Years);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="Speed">Avg Speed over all of them, in kilometres per hour; none without Moving Time.</param>
internal sealed record RideTotalsResponse(int Rides, double Distance, int MovingTime, double? Speed)
{
    public static RideTotalsResponse Of(IReadOnlyCollection<RideResponse> rides)
    {
        var distance = rides.Sum(r => r.Distance);
        var movingTime = rides.Sum(r => r.MovingTime);
        return new RideTotalsResponse(rides.Count, distance, movingTime, ListedActivity.AvgSpeedOf(movingTime, distance));
    }
}

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="Speed">Avg Speed in kilometres per hour.</param>
/// <param name="ElevationGain">Metres.</param>
internal sealed record RideResponse(
    long Id,
    string Name,
    string SportType,
    DateTime StartLocal,
    double Distance,
    int MovingTime,
    double? Speed,
    double ElevationGain,
    string StravaUrl);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="ElapsedTime">Seconds.</param>
/// <param name="Speed">Avg Speed in kilometres per hour; none without Moving Time.</param>
/// <param name="MaxSpeed">Kilometres per hour.</param>
/// <param name="ElevationGain">Metres.</param>
/// <param name="AverageHeartRate">Beats per minute; none without a heart-rate recording.</param>
/// <param name="MaxHeartRate">Beats per minute; none without a heart-rate recording.</param>
/// <param name="AveragePower">Watts, measured or estimated by Strava.</param>
/// <param name="AverageCadence">Pedal revolutions per minute; none without a cadence sensor.</param>
/// <param name="Route">
/// Google's encoded polyline of the route; none for a Ride without GPS, and for a virtual Ride, whose route lies in an
/// app's made-up world.
/// </param>
internal sealed record RideDetailResponse(
    long Id,
    string Name,
    string SportType,
    DateTime StartLocal,
    double Distance,
    int MovingTime,
    int ElapsedTime,
    double? Speed,
    double? MaxSpeed,
    double ElevationGain,
    double? AverageHeartRate,
    double? MaxHeartRate,
    double? AveragePower,
    double? AverageCadence,
    string? Route,
    string StravaUrl)
{
    public static RideDetailResponse From(ListedActivity ride)
    {
        var summary = ride.ReadSummary();
        return new(
            ride.Id,
            ride.Name,
            ride.SportType,
            ride.StartLocal,
            ride.Distance,
            ride.MovingTime,
            ride.ElapsedTime,
            ListedActivity.AvgSpeedOf(ride.MovingTime, ride.Distance),
            summary.MaxSpeed,
            ride.ElevationGain,
            summary.AverageHeartRate,
            summary.MaxHeartRate,
            summary.AveragePower,
            summary.AverageCadence,
            ride.SportType == Sports.VirtualRide ? null : summary.Route,
            StravaLinks.Activity(ride.Id));
    }
}
