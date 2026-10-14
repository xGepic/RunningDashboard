using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// The Runs, Rides and Swims a history list would show, all on one "page" for a CSV export: the same search, date range
/// and sort order, and for Runs also heart rate, Cadence, Relative Effort and the Strava link.
///
/// | Activity         | Type        | Date       | Distance | Moving Time | Elevation | Heart rate | Cadence (one foot) |
/// |------------------|-------------|------------|----------|-------------|-----------|------------|--------------------|
/// | Evening Run      | Run         | 2026-10-06 |  5,081 m |   2,096 s   |    12 m   |   151.4    |        82.5        |
/// | Lauf im Prater   | Run         | 2026-09-28 | 10,020 m |   3,300 s   |     8 m   |   none     |        none        |
/// | Treadmill        | VirtualRun  | 2026-08-30 |  8,000 m |   2,640 s   |     0 m   |   none     |        none        |
/// | Kahlenberg climb | Ride        | 2026-10-05 | 42,300 m |   5,400 s   |   620 m   |            |                    |
/// | Zwift Watopia    | VirtualRide | 2026-10-01 | 30,000 m |   3,600 s   |   310 m   |            |                    |
/// | Lunch Swim       | Swim        | 2026-10-02 |  1,500 m |   1,930 s   |           |            |                    |
/// </summary>
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public sealed class ExportTests
{
    private const long EveningRun = 20479305419;

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync() => _browser = await _app.ConnectWithActivitiesAsync(
        new TestActivity(EveningRun, "Evening Run", new DateTime(2026, 10, 6, 20, 35, 26), 5081, 2096, ElevationGain: 12, RelativeEffort: 41,
            AverageHeartRate: 151.4, AverageCadence: 82.5),
        new TestActivity(20470000001, "Kahlenberg climb", new DateTime(2026, 10, 5, 10, 0, 0), 42300, 5400, "Ride", ElevationGain: 620),
        new TestActivity(20460000002, "Lunch Swim", new DateTime(2026, 10, 2, 12, 0, 0), 1500, 1930, "Swim"),
        new TestActivity(20450000003, "Zwift Watopia", new DateTime(2026, 10, 1, 19, 0, 0), 30000, 3600, "VirtualRide", ElevationGain: 310),
        new TestActivity(20440000004, "Lauf im Prater", new DateTime(2026, 9, 28, 9, 0, 0), 10020, 3300, ElevationGain: 8),
        new TestActivity(20430000005, "Treadmill", new DateTime(2026, 8, 30, 18, 0, 0), 8000, 2640, "VirtualRun"));

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Runs_are_exported_newest_first_with_heart_rate_cadence_relative_effort_and_their_strava_link()
    {
        var runs = await ExportAsync<RunExport>("/api/runs/export");

        runs.Select(r => r.Name).ShouldBe(["Evening Run", "Lauf im Prater", "Treadmill"]);
        runs[0].ShouldBe(new RunExport(
            EveningRun, "Evening Run", "Run", new DateTime(2026, 10, 6, 20, 35, 26), 5081, 2096, runs[0].Pace, 12, 151.4, 165, 41, null,
            $"https://www.strava.com/activities/{EveningRun}"));
        runs[0].Pace!.Value.ShouldBe(412.52, 0.005); // 2,096 s over 5.081 km
        (runs[1].AverageHeartRate, runs[1].Cadence, runs[1].RelativeEffort).ShouldBe((null, null, null));
    }

    [Test]
    public async Task The_export_keeps_the_lists_search_date_range_and_sort_order_but_not_its_page()
    {
        (await ExportAsync<RunExport>("/api/runs/export?from=2026-09-01&to=2026-09-30")).Select(r => r.Name).ShouldBe(["Lauf im Prater"]);
        (await ExportAsync<RunExport>("/api/runs/export?search=LAUF")).Select(r => r.Name).ShouldBe(["Lauf im Prater"]);
        (await ExportAsync<RunExport>("/api/runs/export?sort=pace&direction=asc")).Select(r => r.Name)
            .ShouldBe(["Lauf im Prater", "Treadmill", "Evening Run"]);
        (await ExportAsync<RunExport>("/api/runs/export?page=2&pageSize=1")).Count.ShouldBe(3);
    }

    [Test]
    public async Task An_export_asked_for_with_an_unknown_sort_order_is_refused_as_the_list_would_be()
    {
        (await _browser.GetAsync("/api/runs/export?sort=heartRate")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rides_are_exported_as_listed()
    {
        var rides = await ExportAsync<RideExport>("/api/rides/export?sort=elevation");

        rides.Select(r => (r.Name, r.SportType, r.Distance, r.MovingTime, r.ElevationGain)).ShouldBe(
            [("Kahlenberg climb", "Ride", 42300, 5400, 620), ("Zwift Watopia", "VirtualRide", 30000, 3600, 310)]);
        rides[1].Speed!.Value.ShouldBe(30, 0.0005);
        rides[1].StravaUrl.ShouldBe("https://www.strava.com/activities/20450000003");
    }

    [Test]
    public async Task Swims_are_exported_as_listed()
    {
        var swims = await ExportAsync<SwimExport>("/api/swims/export");

        swims.ShouldHaveSingleItem().ShouldBe(new SwimExport(
            20460000002, "Lunch Swim", new DateTime(2026, 10, 2, 12, 0, 0), 1500, 1930, swims[0].Pace, "https://www.strava.com/activities/20460000002"));
        swims[0].Pace!.Value.ShouldBe(128.67, 0.005); // 1,930 s per 15 × 100 m
    }

    private async Task<List<T>> ExportAsync<T>(string url) => (await _browser.GetFromJsonAsync<List<T>>(url))!;

    private sealed record RunExport(
        long Id,
        string Name,
        string SportType,
        DateTime StartLocal,
        double Distance,
        int MovingTime,
        double? Pace,
        double ElevationGain,
        double? AverageHeartRate,
        double? Cadence,
        double? RelativeEffort,
        string? ShoeName,
        string StravaUrl);

    private sealed record RideExport(long Id, string Name, string SportType, DateTime StartLocal, double Distance, int MovingTime, double? Speed, double ElevationGain, string StravaUrl);

    private sealed record SwimExport(long Id, string Name, DateTime StartLocal, double Distance, int MovingTime, double? Pace, string StravaUrl);
}

/// <summary>Runs imported before heart rate and Cadence had columns get them from their stored summary, without asking Strava.</summary>
public sealed class StoredHeartRateAndCadenceTests
{
    [Test]
    public async Task Runs_stored_before_get_their_heart_rate_and_cadence_from_the_stored_summary()
    {
        var path = Path.Combine(Path.GetTempPath(), $"running-dashboard-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        await using (var db = new AppDbContext(options))
            await db.GetService<IMigrator>().MigrateAsync("20261013122309_AddTheme");
        await using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO Runs (Id, Name, SportType, StartUtc, StartLocal, Distance, MovingTime, ElapsedTime, ElevationGain, RawSummaryJson)
                VALUES (1, 'With heart rate', 'Run', '2026-10-06 18:35:26', '2026-10-06 20:35:26', 5081, 2096, 2096, 12,
                        '{"average_heartrate": 151.4, "average_cadence": 82.5}'),
                       (2, 'Without', 'Run', '2026-10-05 18:00:00', '2026-10-05 20:00:00', 5000, 1800, 1800, 0, '{}');
                """;
            await insert.ExecuteNonQueryAsync();
        }

        var app = new TestApp(path);
        try
        {
            var runs = (await app.CreateBrowser().GetFromJsonAsync<List<HeartRateAndCadence>>("/api/runs/export"))!;

            runs.ShouldBe([new HeartRateAndCadence("With heart rate", 151.4, 165), new HeartRateAndCadence("Without", null, null)]);
        }
        finally
        {
            await app.DisposeAsync();
            File.Delete(path);
            if (Directory.Exists(app.BackupFolder))
                Directory.Delete(app.BackupFolder, recursive: true);
        }
    }

    private sealed record HeartRateAndCadence(string Name, double? AverageHeartRate, double? Cadence);
}
