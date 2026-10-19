using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RunningDashboard.Api.Persistence;
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

        var run = (await browser.GetFromJsonAsync<RunTemperatureResponse>("/api/runs/1/temperature"))!;

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

        var run = (await browser.GetFromJsonAsync<RunTemperatureResponse>("/api/runs/1/temperature"))!;

        run.Temperature.ShouldBe(11);
        _app.Weather.Requests.ShouldHaveSingleItem().Host.ShouldBe("api.open-meteo.com");
    }

    [Test]
    public async Task A_runs_temperature_is_looked_up_once()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Morning Run", new DateTime(2026, 9, 20, 7, 0, 0), 10000, 3600, StartLatLng: Vienna));
        OpenMeteoHasTheDayOf(new DateTime(2026, 9, 20));

        await browser.GetFromJsonAsync<RunTemperatureResponse>("/api/runs/1/temperature");
        var again = (await browser.GetFromJsonAsync<RunTemperatureResponse>("/api/runs/1/temperature"))!;

        again.Temperature.ShouldBe(11);
        _app.Weather.Requests.Count.ShouldBe(1);
    }

    [Test]
    public async Task While_open_meteo_cannot_be_reached_a_run_shows_no_temperature_and_the_next_visit_tries_again()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Morning Run", new DateTime(2026, 9, 20, 7, 0, 0), 10000, 3600, StartLatLng: Vienna));
        _app.Weather.IsDown();

        var whileDown = (await browser.GetFromJsonAsync<RunTemperatureResponse>("/api/runs/1/temperature"))!;
        OpenMeteoHasTheDayOf(new DateTime(2026, 9, 20));
        var later = (await browser.GetFromJsonAsync<RunTemperatureResponse>("/api/runs/1/temperature"))!;

        whileDown.Temperature.ShouldBeNull();
        later.Temperature.ShouldBe(11);
    }

    [Test]
    public async Task An_hour_open_meteo_has_no_value_for_yet_gives_no_temperature()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Morning Run", new DateTime(2026, 9, 20, 7, 0, 0), 10000, 3600, StartLatLng: Vienna));
        _app.Weather.HasHourlyTemperatures(new DateTime(2026, 9, 20), [.. Enumerable.Range(0, 48).Select(hour => hour < 6 ? (double?)hour : null)]);

        var run = (await browser.GetFromJsonAsync<RunTemperatureResponse>("/api/runs/1/temperature"))!;

        run.Temperature.ShouldBeNull();
    }

    [Test]
    public async Task A_run_without_gps_or_on_a_treadmill_has_no_temperature()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Lunch Run", new DateTime(2026, 9, 20, 12, 0, 0), 5000, 1800),
            new TestActivity(2, "Treadmill", new DateTime(2026, 9, 21, 12, 0, 0), 5000, 1800, Trainer: true, StartLatLng: Vienna));
        OpenMeteoHasTheDayOf(new DateTime(2026, 9, 20));

        var withoutGps = (await browser.GetFromJsonAsync<RunTemperatureResponse>("/api/runs/1/temperature"))!;
        var treadmill = (await browser.GetFromJsonAsync<RunTemperatureResponse>("/api/runs/2/temperature"))!;

        withoutGps.Temperature.ShouldBeNull();
        treadmill.Temperature.ShouldBeNull();
        _app.Weather.Requests.ShouldBeEmpty();
    }

    [Test]
    public async Task A_runs_page_loads_without_waiting_for_open_meteo()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Morning Run", new DateTime(2026, 9, 20, 7, 0, 0), 10000, 3600, StartLatLng: Vienna));
        _app.Weather.IsDown();

        var run = await browser.GetAsync("/api/runs/1");

        run.EnsureSuccessStatusCode();
        (await run.Content.ReadAsStringAsync()).ShouldNotContain("temperature");
        _app.Weather.Requests.ShouldBeEmpty();
    }

    [Test]
    public async Task An_unknown_run_has_no_temperature()
    {
        var browser = await _app.ConnectWithActivitiesAsync();

        var response = await browser.GetAsync("/api/runs/99/temperature");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    private sealed record RunTemperatureResponse(double? Temperature);
}

/// <summary>Runs stored before get where they started from the stored summary, for their temperature.</summary>
public sealed class StoredStartLocationTests
{
    [Test]
    public async Task Runs_stored_before_get_their_start_location_from_the_stored_summary()
    {
        var path = Path.Combine(Path.GetTempPath(), $"running-dashboard-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        await using (var db = new AppDbContext(options))
            await db.GetService<IMigrator>().MigrateAsync("20261015061424_AddRunTemperature");
        await using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO Runs (Id, Name, SportType, StartUtc, StartLocal, Distance, MovingTime, ElapsedTime, ElevationGain, RawSummaryJson)
                VALUES (1, 'Outdoors', 'Run', '2026-09-20 05:00:00', '2026-09-20 07:00:00', 10000, 3600, 3600, 0, '{"start_latlng": [48.2, 16.37]}'),
                       (2, 'Without GPS', 'Run', '2026-09-21 05:00:00', '2026-09-21 07:00:00', 10000, 3600, 3600, 0, '{"start_latlng": []}');
                """;
            await insert.ExecuteNonQueryAsync();
        }

        var app = new TestApp(path);
        try
        {
            // 2 °C per hour of the day: halfway through, 05:30 UTC, is 11 °C.
            app.Weather.HasHourlyTemperatures(new DateTime(2026, 9, 20), [.. Enumerable.Range(0, 48).Select(hour => (double?)hour * 2)]);
            var browser = app.CreateBrowser();

            var outdoors = (await browser.GetFromJsonAsync<RunTemperatureResponse>("/api/runs/1/temperature"))!;
            var withoutGps = (await browser.GetFromJsonAsync<RunTemperatureResponse>("/api/runs/2/temperature"))!;

            outdoors.Temperature.ShouldBe(11);
            withoutGps.Temperature.ShouldBeNull();
            app.Weather.Requests.ShouldHaveSingleItem().Query.ShouldContain("latitude=48.2&longitude=16.37");
        }
        finally
        {
            await app.DisposeAsync();
            File.Delete(path);
            if (Directory.Exists(app.BackupFolder))
                Directory.Delete(app.BackupFolder, recursive: true);
        }
    }

    private sealed record RunTemperatureResponse(double? Temperature);
}
