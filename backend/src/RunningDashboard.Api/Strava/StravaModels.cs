namespace RunningDashboard.Api.Strava;

// Strava's JSON as StravaClient reads it (snake_case on the wire). Only the client uses these.

internal sealed record ActivityResponse(
    long Id,
    string Name,
    string SportType,
    DateTimeOffset StartDate,
    DateTimeOffset StartDateLocal,
    double Distance,
    int MovingTime,
    int ElapsedTime,
    double TotalElevationGain,
    double? SufferScore,
    string? GearId,
    double? AverageHeartrate,
    double? AverageCadence,
    bool Trainer);

internal sealed record ShoeResponse(string Id, string Name, double Distance, bool Retired);

internal sealed record ActivityDetailResponse(
    long Id,
    List<BestEffortResponse>? BestEfforts,
    List<SplitResponse>? SplitsMetric,
    List<SplitResponse>? SplitsStandard);

internal sealed record BestEffortResponse(string Name, double Distance, int ElapsedTime);

internal sealed record SplitResponse(
    int Split,
    double Distance,
    int MovingTime,
    int ElapsedTime,
    double? ElevationDifference,
    double? AverageHeartrate,
    double? AverageGradeAdjustedSpeed);

/// <param name="Athlete">Only sent when exchanging an authorization code, not when refreshing.</param>
internal sealed record TokenResponse(string AccessToken, string RefreshToken, long ExpiresAt, AthleteResponse? Athlete);

internal sealed record AthleteResponse(long Id, string Firstname, string Lastname);

// What StravaClient hands to the rest of the app.

public sealed record StravaAthlete(long Id, string FirstName, string LastName);

public sealed record StravaTokenGrant(StravaAthlete Athlete, string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Which Activities to list: started strictly before / after the given UTC times (whole seconds), one page of them.
/// </summary>
public sealed record ActivityQuery(DateTime? Before = null, DateTime? After = null, int Page = 1, int PerPage = 200);

/// <summary>
/// An Activity summary as listed by Strava; <see cref="RawJson"/> is the untouched JSON for later re-derivation.
/// </summary>
/// <param name="AverageHeartRate">Beats per minute; none without a heart-rate recording.</param>
/// <param name="AverageCadence">As Strava counts it for Runs: steps per minute of one foot.</param>
/// <param name="Trainer">Recorded indoors, e.g. on a treadmill.</param>
public sealed record StravaActivity(
    long Id,
    string Name,
    string SportType,
    DateTime StartUtc,
    DateTime StartLocal,
    double Distance,
    int MovingTime,
    int ElapsedTime,
    double ElevationGain,
    double? RelativeEffort,
    string? GearId,
    double? AverageHeartRate,
    double? AverageCadence,
    bool Trainer,
    string RawJson);

/// <summary>
/// A detailed Activity: what the summary lacks. <see cref="RawJson"/> is the untouched JSON.
/// </summary>
/// <param name="KilometreSplits">Strava's splits_metric.</param>
/// <param name="MileSplits">Strava's splits_standard.</param>
public sealed record StravaActivityDetail(
    long Id,
    IReadOnlyList<StravaBestEffort> BestEfforts,
    IReadOnlyList<StravaSplit> KilometreSplits,
    IReadOnlyList<StravaSplit> MileSplits,
    string RawJson);

/// <param name="Name">Strava's name for the distance, e.g. "5K".</param>
public sealed record StravaBestEffort(string Name, double Distance, int ElapsedTime);

/// <param name="Index">1-based.</param>
/// <param name="HeartRate">Average beats per minute; none without a heart-rate recording.</param>
/// <param name="GradeAdjustedSpeed">Metres per second, as if run on the flat.</param>
public sealed record StravaSplit(int Index, double Distance, int MovingTime, int ElapsedTime, double ElevationDifference, double? HeartRate, double? GradeAdjustedSpeed);

/// <param name="Id">Strava's gear id.</param>
/// <param name="Distance">Metres, as Strava counts them.</param>
public sealed record StravaShoe(string Id, string Name, double Distance, bool Retired, string RawJson);
