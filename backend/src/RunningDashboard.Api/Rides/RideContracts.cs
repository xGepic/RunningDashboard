using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Rides;

/// <param name="Totals">Of every Ride that matches, not just this page's.</param>
internal sealed record RidePageResponse(IReadOnlyList<RideResponse> Items, int Page, int PageSize, int TotalCount, RideTotalsResponse Totals);

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
