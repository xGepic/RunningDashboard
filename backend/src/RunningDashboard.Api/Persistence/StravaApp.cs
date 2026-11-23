namespace RunningDashboard.Api.Persistence;

/// <summary>
/// The Strava API app's credentials as entered on the Settings page, for a PC without them in its user secrets, such as
/// another Athlete's own install. There is at most one row.
/// </summary>
public sealed class StravaApp
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public required string ClientId { get; set; }

    public required string ClientSecret { get; set; }
}
