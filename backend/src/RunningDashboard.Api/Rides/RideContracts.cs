namespace RunningDashboard.Api.Rides;

internal sealed record RidePageResponse(IReadOnlyList<RideResponse> Items, int Page, int PageSize, int TotalCount);

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
