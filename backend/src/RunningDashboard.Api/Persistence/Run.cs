using System.Text.Json;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Persistence;

/// <summary>
/// A Strava activity with a running sport type. Rides and Swims are stored apart (see <see cref="Ride"/>, <see cref="Swim"/>); other Activities never.
/// </summary>
public sealed class Run
{
    private static readonly HashSet<string> RunningSportTypes = ["Run", "TrailRun", "VirtualRun"];

    /// <summary>Strava's activity id.</summary>
    public long Id { get; set; }

    public required string Name { get; set; }

    public required string SportType { get; set; }

    public DateTime StartUtc { get; set; }

    /// <summary>Wall-clock start time where the Run happened; decides its Training Week.</summary>
    public DateTime StartLocal { get; set; }

    /// <summary>Metres.</summary>
    public double Distance { get; set; }

    /// <summary>Seconds.</summary>
    public int MovingTime { get; set; }

    /// <summary>Seconds.</summary>
    public int ElapsedTime { get; set; }

    /// <summary>Metres.</summary>
    public double ElevationGain { get; set; }

    public double? RelativeEffort { get; set; }

    /// <summary>Strava's gear id of the Shoe, if one was logged.</summary>
    public string? ShoeId { get; set; }

    public required string RawSummaryJson { get; set; }

    /// <summary>When the detailed Activity was fetched; null while it is still to be fetched.</summary>
    public DateTimeOffset? DetailFetchedAt { get; set; }

    /// <summary>Strava's detailed Activity; null if not fetched yet or Strava no longer had it.</summary>
    public string? RawDetailJson { get; set; }

    public List<BestEffort> BestEfforts { get; } = [];

    public List<Split> Splits { get; } = [];

    public static bool IsRunningSportType(string sportType) => RunningSportTypes.Contains(sportType);

    /// <summary>
    /// Pace: Moving Time per kilometre, in seconds. A Run without distance has none.
    /// </summary>
    public static double? PaceOf(int movingTime, double distance) => PerKilometre(movingTime, distance);

    /// <summary>Seconds per kilometre for <paramref name="seconds"/> over <paramref name="distance"/> metres.</summary>
    public static double? PerKilometre(int seconds, double distance) => distance > 0 ? seconds / (distance / 1000) : null;

    /// <summary>
    /// The route as Strava encodes it (Google's encoded polyline): the detailed Activity's full one, or else the
    /// simplified one from the summary while the detail is still to come. None for a Run without GPS, e.g. on a
    /// treadmill.
    /// </summary>
    public string? Route =>
        PolylineIn(RawDetailJson, "polyline") ?? PolylineIn(RawDetailJson, "summary_polyline") ?? PolylineIn(RawSummaryJson, "summary_polyline");

    /// <summary>
    /// Strava's splits per mile, read from the detailed Activity rather than stored, as only Athletes who read miles
    /// see them. None while the detail is still to come or when Strava no longer has it.
    /// </summary>
    public IReadOnlyList<StravaSplit> MileSplits
    {
        get
        {
            if (RawDetailJson is null)
                return [];
            using var activity = JsonDocument.Parse(RawDetailJson);
            if (!activity.RootElement.TryGetProperty("splits_standard", out var splits) || splits.ValueKind != JsonValueKind.Array)
                return [];
            return
            [
                .. splits.EnumerateArray().Select(s => new StravaSplit(
                    s.GetProperty("split").GetInt32(),
                    s.GetProperty("distance").GetDouble(),
                    s.GetProperty("moving_time").GetInt32(),
                    s.GetProperty("elapsed_time").GetInt32(),
                    s.TryGetProperty("elevation_difference", out var elevation) && elevation.ValueKind == JsonValueKind.Number ? elevation.GetDouble() : 0)),
            ];
        }
    }

    private static string? PolylineIn(string? activityJson, string property)
    {
        if (activityJson is null)
            return null;
        using var activity = JsonDocument.Parse(activityJson);
        return activity.RootElement.TryGetProperty("map", out var map)
            && map.ValueKind == JsonValueKind.Object
            && map.TryGetProperty(property, out var polyline)
            && polyline.GetString() is { Length: > 0 } route
                ? route
                : null;
    }

    public RunDetailStatus DetailStatus => (DetailFetchedAt, RawDetailJson) switch
    {
        (null, _) => RunDetailStatus.Pending,
        (_, null) => RunDetailStatus.Unavailable,
        _ => RunDetailStatus.Loaded,
    };

    public static Run From(StravaActivity activity)
    {
        var run = new Run { Id = activity.Id, Name = activity.Name, SportType = activity.SportType, RawSummaryJson = activity.RawJson };
        run.UpdateFrom(activity);
        return run;
    }

    /// <summary>
    /// Replaces Best Efforts and splits with those of the detailed Activity; null when Strava no longer has it.
    /// </summary>
    public void UseDetail(StravaActivityDetail? detail, DateTimeOffset now)
    {
        DetailFetchedAt = now;
        RawDetailJson = detail?.RawJson;
        BestEfforts.Clear();
        Splits.Clear();
        if (detail is null)
            return;

        BestEfforts.AddRange(detail.BestEfforts.Select(e => new BestEffort { Name = e.Name, Distance = e.Distance, ElapsedTime = e.ElapsedTime }));
        Splits.AddRange(detail.Splits.Select(s => new Split
        {
            Index = s.Index,
            Distance = s.Distance,
            MovingTime = s.MovingTime,
            ElapsedTime = s.ElapsedTime,
            ElevationDifference = s.ElevationDifference,
        }));
    }

    /// <summary>
    /// Takes over the summary. If the distance or times changed (e.g. the Athlete cropped the Activity on Strava),
    /// Best Efforts and splits no longer fit, so the detail is fetched again.
    /// </summary>
    public void UpdateFrom(StravaActivity activity)
    {
        if (Distance != activity.Distance || MovingTime != activity.MovingTime || ElapsedTime != activity.ElapsedTime)
            DetailFetchedAt = null;

        Name = activity.Name;
        SportType = activity.SportType;
        StartUtc = activity.StartUtc;
        StartLocal = activity.StartLocal;
        Distance = activity.Distance;
        MovingTime = activity.MovingTime;
        ElapsedTime = activity.ElapsedTime;
        ElevationGain = activity.ElevationGain;
        RelativeEffort = activity.RelativeEffort;
        ShoeId = activity.GearId;
        RawSummaryJson = activity.RawJson;
    }
}
