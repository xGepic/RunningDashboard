using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

public sealed class RunTemperatureTests
{
    private static readonly double[] Vienna = [48.2, 16.37];

    private readonly TestApp _app = new();

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    /// <summary>2 °C per hour of the day, so 05:00 UTC is 10 °C and 06:00 UTC is 12 °C.</summary>
    private void OpenMeteoHasTheDayOf(DateTime dayUtc) =>
        _app.Weather.HasHourlyTemperatures(dayUtc, [.. Enumerable.Range(0, 48).Select(hour => (double?)hour * 2)]);

    [Test]
    public async Task A_run_has_the_temperature_halfway_through_it_from_the_weather_history_where_it_started()
    {
        // 07:00 in Vienna is 05:00 UTC; halfway through the hour-long Run is 05:30 UTC.
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Morning Run", new DateTime(2026, 9, 20, 7, 0, 0), 10000, 3600, StartLatLng: Vienna));
        OpenMeteoHasTheDayOf(new DateTime(2026, 9, 20));

        var run = (await browser.GetFromJsonAsync<RunDetailResponse>("/api/runs/1"))!;

        run.Temperature.ShouldBe(11);
        var request = _app.Weather.Requests.ShouldHaveSingleItem();
        request.Host.ShouldBe("archive-api.open-meteo.com");
        request.Query.ShouldContain("latitude=48.2&longitude=16.37");
        request.Query.ShouldContain("start_date=2026-09-20&end_date=2026-09-21");
    }

    [Test]
    public async Task A_run_of_the_last_few_days_has_its_temperature_from_the_forecast_which_the_history_does_not_have_yet()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Morning Run", new DateTime(2026, 10, 8, 7, 0, 0), 10000, 3600, StartLatLng: Vienna));
        OpenMeteoHasTheDayOf(new DateTime(2026, 10, 8));

        var run = (await browser.GetFromJsonAsync<RunDetailResponse>("/api/runs/1"))!;

        run.Temperature.ShouldBe(11);
        _app.Weather.Requests.ShouldHaveSingleItem().Host.ShouldBe("api.open-meteo.com");
    }

    [Test]
    public async Task A_runs_temperature_is_looked_up_once()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Morning Run", new DateTime(2026, 9, 20, 7, 0, 0), 10000, 3600, StartLatLng: Vienna));
        OpenMeteoHasTheDayOf(new DateTime(2026, 9, 20));

        await browser.GetFromJsonAsync<RunDetailResponse>("/api/runs/1");
        var again = (await browser.GetFromJsonAsync<RunDetailResponse>("/api/runs/1"))!;

        again.Temperature.ShouldBe(11);
        _app.Weather.Requests.Count.ShouldBe(1);
    }

    [Test]
    public async Task While_open_meteo_cannot_be_reached_a_run_shows_no_temperature_and_the_next_visit_tries_again()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Morning Run", new DateTime(2026, 9, 20, 7, 0, 0), 10000, 3600, StartLatLng: Vienna));
        _app.Weather.IsDown();

        var whileDown = (await browser.GetFromJsonAsync<RunDetailResponse>("/api/runs/1"))!;
        OpenMeteoHasTheDayOf(new DateTime(2026, 9, 20));
        var later = (await browser.GetFromJsonAsync<RunDetailResponse>("/api/runs/1"))!;

        whileDown.Name.ShouldBe("Morning Run");
        whileDown.Temperature.ShouldBeNull();
        later.Temperature.ShouldBe(11);
    }

    [Test]
    public async Task An_hour_open_meteo_has_no_value_for_yet_gives_no_temperature()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Morning Run", new DateTime(2026, 9, 20, 7, 0, 0), 10000, 3600, StartLatLng: Vienna));
        _app.Weather.HasHourlyTemperatures(new DateTime(2026, 9, 20), [.. Enumerable.Range(0, 48).Select(hour => hour < 6 ? (double?)hour : null)]);

        var run = (await browser.GetFromJsonAsync<RunDetailResponse>("/api/runs/1"))!;

        run.Temperature.ShouldBeNull();
    }

    [Test]
    public async Task A_run_without_gps_or_on_a_treadmill_has_no_temperature()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Lunch Run", new DateTime(2026, 9, 20, 12, 0, 0), 5000, 1800),
            new TestActivity(2, "Treadmill", new DateTime(2026, 9, 21, 12, 0, 0), 5000, 1800, Trainer: true, StartLatLng: Vienna));
        OpenMeteoHasTheDayOf(new DateTime(2026, 9, 20));

        var withoutGps = (await browser.GetFromJsonAsync<RunDetailResponse>("/api/runs/1"))!;
        var treadmill = (await browser.GetFromJsonAsync<RunDetailResponse>("/api/runs/2"))!;

        withoutGps.Temperature.ShouldBeNull();
        treadmill.Temperature.ShouldBeNull();
        _app.Weather.Requests.ShouldBeEmpty();
    }

    private sealed record RunDetailResponse(long Id, string Name, double? Temperature);
}
