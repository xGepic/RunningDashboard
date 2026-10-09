using System.ComponentModel.DataAnnotations;

namespace RunningDashboard.Api.Strava;

/// <summary>
/// The Strava API app's credentials. ClientId and ClientSecret come from user secrets, never from committed config.
/// </summary>
public sealed class StravaOptions
{
    public const string Section = "Strava";

    [Required(ErrorMessage = "Strava:ClientId is missing. Set it with: dotnet user-secrets set \"Strava:ClientId\" \"<your client id>\"")]
    public string ClientId { get; set; } = "";

    [Required(ErrorMessage = "Strava:ClientSecret is missing. Set it with: dotnet user-secrets set \"Strava:ClientSecret\" \"<your client secret>\"")]
    public string ClientSecret { get; set; } = "";

    [Required]
    public string RedirectUri { get; set; } = "";
}
