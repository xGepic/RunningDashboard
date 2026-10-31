using System.Text.Json;

namespace RunningDashboard.Api.Persistence;

/// <summary>Reads what is not in its own column from an Activity's stored Strava JSON, summary or detail.</summary>
internal static class ActivityJson
{
    /// <summary>The number <paramref name="property"/>; none if Strava sent none.</summary>
    public static double? NumberIn(JsonElement activity, string property) =>
        activity.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    /// <summary>
    /// The route under map.<paramref name="property"/>, as Strava encodes it (Google's encoded polyline); none if it
    /// is missing or empty, as for an Activity without GPS.
    /// </summary>
    public static string? PolylineIn(JsonElement activity, string property) =>
        activity.TryGetProperty("map", out var map)
        && map.ValueKind == JsonValueKind.Object
        && map.TryGetProperty(property, out var polyline)
        && polyline.ValueKind == JsonValueKind.String
        && polyline.GetString() is { Length: > 0 } route
            ? route
            : null;
}
