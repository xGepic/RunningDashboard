using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Swims;

/// <param name="Totals">Of every Swim that matches, not just this page's.</param>
internal sealed record SwimPageResponse(IReadOnlyList<SwimResponse> Items, int Page, int PageSize, int TotalCount, SwimTotalsResponse Totals);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="Pace">Swim Pace over all of them, in seconds per 100 metres; none without distance.</param>
internal sealed record SwimTotalsResponse(int Swims, double Distance, int MovingTime, double? Pace)
{
    public static SwimTotalsResponse Of(IReadOnlyCollection<SwimResponse> swims)
    {
        var distance = swims.Sum(s => s.Distance);
        var movingTime = swims.Sum(s => s.MovingTime);
        return new SwimTotalsResponse(swims.Count, distance, movingTime, ListedActivity.SwimPaceOf(movingTime, distance));
    }
}

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="Pace">Swim Pace in seconds per 100 metres.</param>
internal sealed record SwimResponse(
    long Id,
    string Name,
    DateTime StartLocal,
    double Distance,
    int MovingTime,
    double? Pace,
    string StravaUrl);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="ElapsedTime">Seconds.</param>
/// <param name="Pace">Swim Pace in seconds per 100 metres; none without distance.</param>
/// <param name="AverageHeartRate">Beats per minute; none without a heart-rate recording.</param>
/// <param name="MaxHeartRate">Beats per minute; none without a heart-rate recording.</param>
internal sealed record SwimDetailResponse(
    long Id,
    string Name,
    DateTime StartLocal,
    double Distance,
    int MovingTime,
    int ElapsedTime,
    double? Pace,
    double? AverageHeartRate,
    double? MaxHeartRate,
    string StravaUrl)
{
    public static SwimDetailResponse From(ListedActivity swim) => new(
        swim.Id,
        swim.Name,
        swim.StartLocal,
        swim.Distance,
        swim.MovingTime,
        swim.ElapsedTime,
        ListedActivity.SwimPaceOf(swim.MovingTime, swim.Distance),
        swim.AverageHeartRate,
        swim.MaxHeartRate,
        $"https://www.strava.com/activities/{swim.Id}");
}
