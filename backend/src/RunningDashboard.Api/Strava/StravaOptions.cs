namespace RunningDashboard.Api.Strava;

/// <summary>
/// The Strava API app's credentials from configuration (user secrets in development, never committed config). Without
/// them, the ones entered on the Settings page apply; see <see cref="StravaAppCredentials"/>.
/// </summary>
public sealed class StravaOptions
{
    public const string Section = "Strava";

    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Where Strava sends the browser back to. Without it, the callback on the host the browser came from, which is
    /// right whenever the backend serves the frontend itself.
    /// </summary>
    public string RedirectUri { get; set; } = "";
}
