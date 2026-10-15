using System.Net;
using System.Text;
using System.Text.Json;

namespace RunningDashboard.Api.Tests.Harness;

/// <summary>
/// Stands in for Open-Meteo at the HTTP level; every request the app sends is kept for assertions. Until a test gives
/// it hourly temperatures it is down (503), so a Run's page shows no temperature.
/// </summary>
public sealed class FakeOpenMeteo : HttpMessageHandler
{
    private Func<HttpResponseMessage> _respond = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

    public List<Uri> Requests { get; } = [];

    /// <summary>Answers every request with one temperature (°C) per hour from <paramref name="firstHourUtc"/> on.</summary>
    public void HasHourlyTemperatures(DateTime firstHourUtc, params double?[] temperatures)
    {
        var start = new DateTimeOffset(firstHourUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        var json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["latitude"] = 48.2,
            ["longitude"] = 16.4,
            ["hourly_units"] = new Dictionary<string, string> { ["time"] = "unixtime", ["temperature_2m"] = "°C" },
            ["hourly"] = new Dictionary<string, object>
            {
                ["time"] = temperatures.Select((_, i) => start + i * 3600),
                ["temperature_2m"] = temperatures,
            },
        });
        _respond = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    public void IsDown() => _respond = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
            Requests.Add(request.RequestUri!);
        return Task.FromResult(_respond());
    }
}
