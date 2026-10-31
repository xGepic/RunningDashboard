using System.Text.Json;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Persistence;

/// <summary>
/// A Strava activity with a running sport type (see <see cref="Sports.Runs"/>). Rides and Swims are stored apart (see <see cref="ListedActivity"/>); other Activities never.
/// </summary>
public sealed class Run
{
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

    /// <summary>Beats per minute; none for a Run recorded without heart rate.</summary>
    public double? AverageHeartRate { get; set; }

    /// <summary>Cadence: steps per minute, both feet together; none for a Run recorded without it.</summary>
    public double? Cadence { get; set; }

    /// <summary>Recorded indoors, as Strava marks it, e.g. on a treadmill.</summary>
    public bool Trainer { get; set; }

    /// <summary>On a treadmill or virtual: its Pace is unreliable, so it is no Fitness Run.</summary>
    public bool IsTreadmill => IsTreadmillRun(Trainer, SportType);

    public static bool IsTreadmillRun(bool trainer, string sportType) => trainer || sportType == Sports.VirtualRun;

    /// <summary>Strava's gear id of the Shoe, if one was logged.</summary>
    public string? ShoeId { get; set; }

    /// <summary>
    /// Air temperature in °C halfway through the Run, from Open-Meteo's weather history at its start; null until it is
    /// looked up, and for a Run without one (see <see cref="WeatherLocation"/>).
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>Halfway through the Run, in UTC: when its temperature is looked up.</summary>
    public DateTime MidpointUtc => StartUtc.AddSeconds(ElapsedTime / 2.0);

    /// <summary>Where the Run started, as Strava recorded it; none for a Run without GPS.</summary>
    public double? StartLatitude { get; set; }

    /// <inheritdoc cref="StartLatitude"/>
    public double? StartLongitude { get; set; }

    /// <summary>
    /// Where the Run started, for looking up the weather. None for a Run without GPS, and for one on a treadmill,
    /// where the weather outside does not matter.
    /// </summary>
    public (double Latitude, double Longitude)? WeatherLocation =>
        !IsTreadmill && StartLatitude is { } latitude && StartLongitude is { } longitude ? (latitude, longitude) : null;

    public required string RawSummaryJson { get; set; }

    /// <summary>When the detailed Activity was fetched; null while it is still to be fetched.</summary>
    public DateTimeOffset? DetailFetchedAt { get; set; }

    /// <summary>Strava's detailed Activity; null if not fetched yet or Strava no longer had it.</summary>
    public string? RawDetailJson { get; set; }

    public List<BestEffort> BestEfforts { get; } = [];

    public List<Split> Splits { get; } = [];

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
    public string? Route => RouteIn(RawDetailJson, "polyline", "summary_polyline") ?? RouteIn(RawSummaryJson, "summary_polyline");

    /// <summary>The first route found under map.<paramref name="properties"/>, in that order; none without JSON.</summary>
    private static string? RouteIn(string? activityJson, params string[] properties)
    {
        if (activityJson is null)
            return null;
        using var json = JsonDocument.Parse(activityJson);
        return properties.Select(p => ActivityJson.PolylineIn(json.RootElement, p)).FirstOrDefault(route => route is not null);
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
        Splits.AddRange(SplitsOf(detail.KilometreSplits, SplitUnit.Kilometre));
        Splits.AddRange(SplitsOf(detail.MileSplits, SplitUnit.Mile));
    }

    private static IEnumerable<Split> SplitsOf(IEnumerable<StravaSplit> splits, SplitUnit unit) =>
        splits.Select(s => new Split
        {
            Unit = unit,
            Index = s.Index,
            Distance = s.Distance,
            MovingTime = s.MovingTime,
            ElapsedTime = s.ElapsedTime,
            ElevationDifference = s.ElevationDifference,
            HeartRate = s.HeartRate,
            GradeAdjustedSpeed = s.GradeAdjustedSpeed,
        });

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
        AverageHeartRate = activity.AverageHeartRate;
        // Strava counts one foot's steps.
        Cadence = activity.AverageCadence * 2;
        Trainer = activity.Trainer;
        ShoeId = activity.GearId;
        StartLatitude = activity.StartLatitude;
        StartLongitude = activity.StartLongitude;
        RawSummaryJson = activity.RawJson;
    }
}
