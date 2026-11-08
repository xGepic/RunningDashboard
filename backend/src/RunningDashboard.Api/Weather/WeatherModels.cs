using System.Text.Json.Serialization;

namespace RunningDashboard.Api.Weather;

// Open-Meteo's JSON as WeatherClient reads it. Only the client uses these.

internal sealed record WeatherResponse(HourlyResponse? Hourly);

/// <param name="Time">Unix seconds of each hour.</param>
/// <param name="Temperature">°C at each hour; null where Open-Meteo has none yet.</param>
internal sealed record HourlyResponse(
    IReadOnlyList<long> Time,
    [property: JsonPropertyName("temperature_2m")] IReadOnlyList<double?> Temperature);
