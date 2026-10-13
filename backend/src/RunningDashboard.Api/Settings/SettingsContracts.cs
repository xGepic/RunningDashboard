using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Settings;

/// <summary>The settings to change; the ones left out stay as they are.</summary>
/// <param name="DistanceUnit">km or mi.</param>
/// <param name="ShoeMaximumDistance">Metres.</param>
internal sealed record SettingsRequest(string? DistanceUnit, double? ShoeMaximumDistance);

/// <param name="DistanceUnit">km or mi.</param>
/// <param name="ShoeMaximumDistance">Metres.</param>
internal sealed record SettingsResponse(string DistanceUnit, double ShoeMaximumDistance)
{
    public static SettingsResponse From(AthleteSettings settings) => new(settings.DistanceUnit, settings.ShoeMaximumDistance);
}
