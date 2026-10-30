using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Settings;

/// <summary>The settings to change; the ones left out stay as they are.</summary>
/// <param name="DistanceUnit">km or mi.</param>
/// <param name="ShoeMaximumDistance">Metres.</param>
/// <param name="Theme">blue, teal, green, orange, red, pink or purple.</param>
/// <param name="ShowYearlyGoal">Whether the Overview shows the Yearly Goal.</param>
/// <param name="ShowWeeklyTimeGoal">Whether the Overview shows the Weekly Time Goal.</param>
internal sealed record SettingsRequest(string? DistanceUnit, double? ShoeMaximumDistance, string? Theme, bool? ShowYearlyGoal, bool? ShowWeeklyTimeGoal);

/// <param name="DistanceUnit">km or mi.</param>
/// <param name="ShoeMaximumDistance">Metres.</param>
/// <param name="Theme">blue, teal, green, orange, red, pink or purple.</param>
/// <param name="ShowYearlyGoal">Whether the Overview shows the Yearly Goal.</param>
/// <param name="ShowWeeklyTimeGoal">Whether the Overview shows the Weekly Time Goal.</param>
internal sealed record SettingsResponse(string DistanceUnit, double ShoeMaximumDistance, string Theme, bool ShowYearlyGoal, bool ShowWeeklyTimeGoal)
{
    public static SettingsResponse From(AthleteSettings settings) =>
        new(settings.DistanceUnit, settings.ShoeMaximumDistance, settings.Theme, settings.ShowYearlyGoal, settings.ShowWeeklyTimeGoal);
}
