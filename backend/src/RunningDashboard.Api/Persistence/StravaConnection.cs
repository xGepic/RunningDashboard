using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Persistence;

/// <summary>
/// The app's link to the Athlete's Strava account. There is at most one row.
/// </summary>
public sealed class StravaConnection
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public long AthleteId { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public required string AccessToken { get; set; }

    public required string RefreshToken { get; set; }

    public DateTimeOffset AccessTokenExpiresAt { get; set; }

    public DateTimeOffset ConnectedAt { get; set; }

    public static StravaConnection Create(StravaTokenGrant grant, DateTimeOffset now) => new()
    {
        AthleteId = grant.Athlete.Id,
        FirstName = grant.Athlete.FirstName,
        LastName = grant.Athlete.LastName,
        AccessToken = grant.AccessToken,
        RefreshToken = grant.RefreshToken,
        AccessTokenExpiresAt = grant.ExpiresAt,
        ConnectedAt = now,
    };

    /// <summary>
    /// Takes over new tokens (and any name change) for the same athlete; ConnectedAt keeps the first connection time.
    /// </summary>
    public void Refresh(StravaTokenGrant grant)
    {
        if (grant.Athlete.Id != AthleteId)
            throw new InvalidOperationException("Tokens belong to a different Strava athlete.");

        FirstName = grant.Athlete.FirstName;
        LastName = grant.Athlete.LastName;
        AccessToken = grant.AccessToken;
        RefreshToken = grant.RefreshToken;
        AccessTokenExpiresAt = grant.ExpiresAt;
    }
}
