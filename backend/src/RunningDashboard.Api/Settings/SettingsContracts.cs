using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Settings;

/// <param name="DistanceUnit">km or mi.</param>
internal sealed record SettingsRequest(string? DistanceUnit);

/// <param name="DistanceUnit">km or mi.</param>
internal sealed record SettingsResponse(string DistanceUnit)
{
    public static SettingsResponse From(AthleteSettings settings) => new(settings.DistanceUnit);
}
