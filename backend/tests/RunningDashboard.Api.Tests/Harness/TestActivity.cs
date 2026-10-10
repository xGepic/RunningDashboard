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
    int? ElapsedTime = null)
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
        detail["splits_metric"] = Array.Empty<object>();
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
            ["trainer"] = SportType == "VirtualRun",
            ["commute"] = false,
            ["manual"] = false,
            ["private"] = false,
            ["visibility"] = "everyone",
            ["gear_id"] = null,
            ["average_speed"] = MovingTime > 0 ? Distance / MovingTime : 0,
            ["has_heartrate"] = false,
            ["pr_count"] = 0,
            ["suffer_score"] = null,
        };
    }
}

/// <summary>Strava's fastest time over a standard distance within a <see cref="TestActivity"/>.</summary>
/// <param name="Name">Strava's name for the distance, e.g. "5K" or "Half-Marathon".</param>
public sealed record TestBestEffort(string Name, double Distance, int ElapsedTime);
