using System.Globalization;
using System.Text.Json;

namespace RunningDashboard.Api.Tests.Harness;

/// <summary>
/// A Strava Activity summary for tests that need specific dates, distances or times. <see cref="ToJson"/> renders
/// it the way Strava lists Activities, <see cref="ToDetailJson"/> as Strava's detailed Activity with the given Best
/// Efforts; fields a test does not set get plausible values.
/// </summary>
/// <param name="StartLocal">Wall-clock start where the Activity happened.</param>
/// <param name="UtcOffsetHours">Offset of that place from UTC, e.g. 2 for Vienna in summer, -4 for New York.</param>
/// <param name="ElapsedTime">Seconds; the Moving Time unless a test sets it.</param>
/// <param name="RelativeEffort">Strava's suffer_score field; none unless a test sets it.</param>
/// <param name="AverageHeartRate">Beats per minute; none (recorded without heart rate) unless a test sets it.</param>
/// <param name="Splits">Strava's km splits in the detail; none unless a test sets them.</param>
/// <param name="Trainer">Recorded indoors, e.g. on a treadmill.</param>
/// <param name="GearId">Strava's gear id of the Shoe; none unless a test sets it.</param>
/// <param name="AverageCadence">As Strava counts it: one foot's steps per minute; none unless a test sets it.</param>
/// <param name="StartLatLng">Where it started, latitude then longitude; none (recorded without GPS) unless a test sets it.</param>
public sealed record TestActivity(
    long Id,
    string Name,
    DateTime StartLocal,
    double Distance,
    int MovingTime,
    string SportType = "Run",
    double UtcOffsetHours = 2,
    double ElevationGain = 0,
    TestBestEffort[]? BestEfforts = null,
    int? ElapsedTime = null,
    int? RelativeEffort = null,
    double? AverageHeartRate = null,
    double? AverageCadence = null,
    string? GearId = null,
    TestSplit[]? Splits = null,
    bool Trainer = false,
    double[]? StartLatLng = null)
{
    public static string ToJson(IEnumerable<TestActivity> activities) =>
        JsonSerializer.Serialize(activities.Select(a => a.ToStrava()));

    public string ToDetailJson()
    {
        var detail = ToStrava();
        detail["resource_state"] = 3;
        detail["best_efforts"] = (BestEfforts ?? []).Select(e => new Dictionary<string, object?>
        {
            ["name"] = e.Name,
            ["distance"] = e.Distance,
            ["elapsed_time"] = e.ElapsedTime,
            ["moving_time"] = e.ElapsedTime,
        });
        detail["splits_metric"] = (Splits ?? []).Select((s, i) => new Dictionary<string, object?>
        {
            ["split"] = i + 1,
            ["distance"] = s.Distance,
            ["moving_time"] = s.MovingTime,
            ["elapsed_time"] = s.MovingTime,
            ["elevation_difference"] = 0,
            ["average_heartrate"] = s.HeartRate,
            ["average_grade_adjusted_speed"] = s.GradeAdjustedSpeed,
        });
        return JsonSerializer.Serialize(detail);
    }

    private Dictionary<string, object?> ToStrava()
    {
        var startUtc = StartLocal.AddHours(-UtcOffsetHours);
        return new Dictionary<string, object?>
        {
            ["resource_state"] = 2,
            ["athlete"] = new { id = 133415890, resource_state = 1 },
            ["name"] = Name,
            ["distance"] = Distance,
            ["moving_time"] = MovingTime,
            ["elapsed_time"] = ElapsedTime ?? MovingTime,
            ["total_elevation_gain"] = ElevationGain,
            ["type"] = SportType == "TrailRun" ? "Run" : SportType,
            ["sport_type"] = SportType,
            ["workout_type"] = 0,
            ["id"] = Id,
            // Strava writes both with "Z", although start_date_local is the local wall-clock time.
            ["start_date"] = startUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["start_date_local"] = StartLocal.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["timezone"] = $"(GMT{UtcOffsetHours:+00;-00}:00) Test/Zone",
            ["utc_offset"] = UtcOffsetHours * 3600,
            ["start_latlng"] = StartLatLng ?? [],
            ["trainer"] = Trainer || SportType == "VirtualRun",
            ["commute"] = false,
            ["manual"] = false,
            ["private"] = false,
            ["visibility"] = "everyone",
            ["gear_id"] = GearId,
            ["average_speed"] = MovingTime > 0 ? Distance / MovingTime : 0,
            ["has_heartrate"] = AverageHeartRate is not null,
            ["average_heartrate"] = AverageHeartRate,
            ["average_cadence"] = AverageCadence,
            ["pr_count"] = 0,
            ["suffer_score"] = RelativeEffort,
        };
    }
}

/// <summary>Strava's fastest time over a standard distance within a <see cref="TestActivity"/>.</summary>
/// <param name="Name">Strava's name for the distance, e.g. "5K" or "Half-Marathon".</param>
public sealed record TestBestEffort(string Name, double Distance, int ElapsedTime);

/// <summary>One km split of a <see cref="TestActivity"/>'s detail.</summary>
/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="HeartRate">Beats per minute.</param>
/// <param name="GradeAdjustedSpeed">Metres per second.</param>
public sealed record TestSplit(double Distance, int MovingTime, double? HeartRate, double? GradeAdjustedSpeed);
