using System.Globalization;
using System.Text.Json;

namespace RunningDashboard.Api.Weather;

/// <summary>
/// The only place that talks to Open-Meteo (open-meteo.com, free and without an API key) for the weather, which Strava
/// does not have. Only a place and a time are sent.
/// </summary>
public sealed class WeatherClient(HttpClient http, TimeProvider clock, ILogger<WeatherClient> logger)
{
    /// <summary>Hourly weather history since 1940, a few days behind.</summary>
    private const string ArchiveUrl = "https://archive-api.open-meteo.com/v1/archive";

    /// <summary>The days the archive does not have yet.</summary>
    private const string ForecastUrl = "https://api.open-meteo.com/v1/forecast";

    private static readonly TimeSpan ArchiveDelay = TimeSpan.FromDays(7);

    /// <summary>
    /// The air temperature (°C, 2 m above ground) at <paramref name="utc"/>, between the two hourly values around it.
    /// None if Open-Meteo has no value yet or cannot be reached; that is logged, so the caller can just try again later.
    /// </summary>
    public async Task<double?> TemperatureAtAsync(double latitude, double longitude, DateTime utc, CancellationToken cancellationToken)
    {
        try
        {
            var hourly = await HourlyAsync(latitude, longitude, DateOnly.FromDateTime(utc), cancellationToken);
            return Between(hourly, new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds());
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(exception, "Could not get the temperature at {Utc} from Open-Meteo", utc);
            return null;
        }
    }

    /// <summary>The hours of <paramref name="day"/> and the next, so a time just before midnight has an hour after it.</summary>
    private async Task<HourlyResponse> HourlyAsync(double latitude, double longitude, DateOnly day, CancellationToken cancellationToken)
    {
        var archived = clock.GetUtcNow().UtcDateTime - day.ToDateTime(TimeOnly.MinValue) > ArchiveDelay;
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{(archived ? ArchiveUrl : ForecastUrl)}?latitude={latitude}&longitude={longitude}&hourly=temperature_2m"
            + $"&start_date={day:yyyy-MM-dd}&end_date={day.AddDays(1):yyyy-MM-dd}&timezone=GMT&timeformat=unixtime");
        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var weather = await response.Content.ReadFromJsonAsync<WeatherResponse>(cancellationToken);
        return weather?.Hourly ?? throw new JsonException("Open-Meteo sent no hourly values.");
    }

    private static double? Between(HourlyResponse hourly, long unixTime)
    {
        for (var i = 0; i + 1 < hourly.Time.Count; i++)
        {
            if (hourly.Time[i] > unixTime || hourly.Time[i + 1] <= unixTime)
                continue;
            if (hourly.Temperature[i] is not { } before || hourly.Temperature[i + 1] is not { } after)
                return null;
            var fraction = (double)(unixTime - hourly.Time[i]) / (hourly.Time[i + 1] - hourly.Time[i]);
            return Math.Round(before + (after - before) * fraction, 1);
        }

        return null;
    }
}
