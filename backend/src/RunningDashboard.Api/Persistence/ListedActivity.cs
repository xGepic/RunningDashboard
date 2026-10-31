using System.Text.Json;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Persistence;

/// <summary>
/// A Ride or a Swim: an Activity that is only listed, in the Ride or Swim history. Listed Activities never count
/// toward the running figures. Which sport one is follows from its Strava sport type (see <see cref="Sports"/>).
/// </summary>
public sealed class ListedActivity
{
    /// <summary>Strava's activity id.</summary>
    public long Id { get; set; }

    public required string Name { get; set; }

    public required string SportType { get; set; }

    public DateTime StartUtc { get; set; }

    /// <summary>Wall-clock start time where the Activity happened.</summary>
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

    /// <summary>What its page shows beyond the columns, from the stored summary, read in one go.</summary>
    public ListedActivitySummary ReadSummary()
    {
        using var json = JsonDocument.Parse(RawSummaryJson);
        var activity = json.RootElement;
        return new ListedActivitySummary(
            ActivityJson.NumberIn(activity, "average_heartrate"),
            ActivityJson.NumberIn(activity, "max_heartrate"),
            ActivityJson.NumberIn(activity, "max_speed") * 3.6,
            ActivityJson.NumberIn(activity, "average_watts"),
            ActivityJson.NumberIn(activity, "average_cadence"),
            ActivityJson.PolylineIn(activity, "summary_polyline"));
    }

    /// <summary>
    /// Avg Speed: distance per Moving Time, in kilometres per hour. An Activity without Moving Time has none.
    /// </summary>
    public static double? AvgSpeedOf(int movingTime, double distance) => movingTime > 0 ? distance / 1000 / (movingTime / 3600.0) : null;

    /// <summary>
    /// Swim Pace: Moving Time per 100 metres, in seconds. An Activity without distance has none.
    /// </summary>
    public static double? SwimPaceOf(int movingTime, double distance) => distance > 0 ? movingTime / (distance / 100) : null;

    public static ListedActivity From(StravaActivity activity)
    {
        var listed = new ListedActivity { Id = activity.Id, Name = activity.Name, SportType = activity.SportType, RawSummaryJson = activity.RawJson };
        listed.UpdateFrom(activity);
        return listed;
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
