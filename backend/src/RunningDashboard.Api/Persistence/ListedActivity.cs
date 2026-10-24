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

    /// <summary>Beats per minute, from the stored summary; none without a heart-rate recording.</summary>
    public double? AverageHeartRate => NumberIn(RawSummaryJson, "average_heartrate");

    /// <inheritdoc cref="AverageHeartRate"/>
    public double? MaxHeartRate => NumberIn(RawSummaryJson, "max_heartrate");

    /// <summary>Kilometres per hour, from the stored summary; none if Strava sent none.</summary>
    public double? MaxSpeed => NumberIn(RawSummaryJson, "max_speed") * 3.6;

    /// <summary>Watts, from the stored summary: measured with a power meter, or else Strava's estimate for a Ride; none for most Swims.</summary>
    public double? AveragePower => NumberIn(RawSummaryJson, "average_watts");

    /// <summary>Pedal revolutions per minute for a Ride, from the stored summary; none without a cadence sensor.</summary>
    public double? AverageCadence => NumberIn(RawSummaryJson, "average_cadence");

    /// <summary>
    /// The simplified route from the stored summary, as Strava encodes it (Google's encoded polyline). None for an
    /// Activity without GPS, such as a Swim in a pool.
    /// </summary>
    public string? Route
    {
        get
        {
            using var activity = JsonDocument.Parse(RawSummaryJson);
            return activity.RootElement.TryGetProperty("map", out var map)
                && map.ValueKind == JsonValueKind.Object
                && map.TryGetProperty("summary_polyline", out var polyline)
                && polyline.ValueKind == JsonValueKind.String
                && polyline.GetString() is { Length: > 0 } route
                    ? route
                    : null;
        }
    }

    private static double? NumberIn(string activityJson, string property)
    {
        using var activity = JsonDocument.Parse(activityJson);
        return activity.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
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
