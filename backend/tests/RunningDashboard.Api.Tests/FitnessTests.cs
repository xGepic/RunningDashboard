using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Aerobic Efficiency, Cardiac Drift and Cadence of the Fitness Runs: every Run except treadmill ones and those on a
/// Goal Race's race day. Cardiac Drift only for a week's Long Run of at least 10 km.
///
/// | Run            | Date           | Distance | Splits (km)                                   | Notes                     |
/// |----------------|----------------|----------|-----------------------------------------------|---------------------------|
/// | Easy           | Tue 2026-09-08 |  8,000 m | 8 × 360 s, 2.5 m/s grade-adjusted, 140 bpm     | not the week's Long Run   |
/// | Long           | Sun 2026-09-13 | 12,000 m | 12 × 400 s, 2.5 m/s; 140 bpm, then 147 bpm      | the Long Run              |
/// | Treadmill      | Wed 2026-09-16 |  8,000 m |                                               | no Fitness Run            |
/// | Short Long Run | Sat 2026-09-19 |  9,000 m | 9 × 400 s, 2.5 m/s, 150 bpm                    | Long Run, but under 10 km |
/// | Shakeout       | Fri 2026-09-25 |  5,000 m | 5 × 400 s, 2.5 m/s, no heart rate              |                           |
/// | Marathon       | Sun 2026-09-27 | 42,195 m |                                               | Goal Race: no Fitness Run |
/// | Straddle       | Sun 2026-10-04 | 10,500 m | 10 × 400 s and 1 × 200 s over 500 m, 2.5 m/s    | the middle (5,250 m) is a quarter into split 6 |
/// </summary>
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public sealed class FitnessTests
{
    private const long Easy = 1;
    private const long Long = 2;
    private const long Treadmill = 3;
    private const long ShortLongRun = 4;
    private const long Shakeout = 5;
    private const long Straddle = 7;

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        static TestSplit[] Splits(int count, int movingTime, double? heartRate) =>
            [.. Enumerable.Range(0, count).Select(_ => new TestSplit(1000, movingTime, heartRate, 2.5))];

        _browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(Easy, "Easy", new DateTime(2026, 9, 8, 18, 0, 0), 8000, 2880, AverageHeartRate: 140, AverageCadence: 84, BestEfforts: [],
                Splits: [.. Enumerable.Range(0, 8).Select(_ => new TestSplit(1000, 360, 140, 1000.0 / 360))]),
            new TestActivity(Long, "Long", new DateTime(2026, 9, 13, 8, 0, 0), 12000, 4800, AverageHeartRate: 143.5, AverageCadence: 82, BestEfforts: [],
                Splits: [.. Splits(6, 400, 140), .. Splits(6, 400, 147)]),
            new TestActivity(Treadmill, "Treadmill", new DateTime(2026, 9, 16, 19, 0, 0), 8000, 2900, AverageHeartRate: 150, Trainer: true),
            new TestActivity(ShortLongRun, "Short Long Run", new DateTime(2026, 9, 19, 8, 0, 0), 9000, 3600, AverageHeartRate: 150, BestEfforts: [],
                Splits: Splits(9, 400, 150)),
            new TestActivity(Shakeout, "Shakeout", new DateTime(2026, 9, 25, 7, 0, 0), 5000, 2000, BestEfforts: [], Splits: Splits(5, 400, null)),
            new TestActivity(6, "Marathon", new DateTime(2026, 9, 27, 9, 0, 0), 42195, 17000, AverageHeartRate: 160),
            new TestActivity(Straddle, "Straddle", new DateTime(2026, 10, 4, 8, 0, 0), 10500, 4200, AverageHeartRate: 147, BestEfforts: [],
                Splits: [.. Splits(5, 400, 140), .. Splits(5, 400, 150), new TestSplit(500, 200, 150, 2.5)]));

        var race = await _browser.PostAsJsonAsync("/api/goal-races", new { name = "Autumn Marathon", date = "2026-09-27", distance = 42195, trainingBlockWeeks = 12 });
        race.EnsureSuccessStatusCode();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Treadmill_runs_and_runs_on_race_day_are_no_fitness_runs()
    {
        (await FitnessAsync()).Runs.Select(r => r.Name).ShouldBe(["Easy", "Long", "Short Long Run", "Shakeout", "Straddle"]);
    }

    [Test]
    public async Task Aerobic_efficiency_is_grade_adjusted_metres_per_minute_over_the_average_heart_rate()
    {
        var runs = (await FitnessAsync()).Runs.ToDictionary(r => r.Id);

        runs[Easy].AerobicEfficiency!.Value.ShouldBe(1000.0 / 360 * 60 / 140, 1e-9); // 1.19 m per beat
        runs[Long].AerobicEfficiency!.Value.ShouldBe(2.5 * 60 / 143.5, 1e-9);
        runs[Shakeout].AerobicEfficiency.ShouldBeNull();
    }

    [Test]
    public async Task Cardiac_drift_compares_the_second_half_of_a_long_run_of_10_km_or_more_with_the_first()
    {
        var runs = (await FitnessAsync()).Runs.ToDictionary(r => r.Id);

        runs[Long].CardiacDrift!.Value.ShouldBe(0.05, 1e-9); // 147 against 140 bpm at the same speed
        runs[Easy].CardiacDrift.ShouldBeNull();
        runs[ShortLongRun].CardiacDrift.ShouldBeNull();
    }

    [Test]
    public async Task A_split_across_the_middle_counts_in_both_halves_by_its_distance()
    {
        var straddle = (await FitnessAsync()).Runs.Single(r => r.Id == Straddle);

        // First half: 5 splits at 140 bpm and a quarter of one at 150; second half: the rest at 150, same speed.
        var first = (5 * 140 * 400 + 0.25 * 150 * 400) / (5 * 1000 + 0.25 * 1000);
        var second = (0.75 * 150 * 400 + 4 * 150 * 400 + 150 * 200) / (0.75 * 1000 + 4 * 1000 + 500);
        straddle.CardiacDrift!.Value.ShouldBe(second / first - 1, 1e-9);
    }

    [Test]
    public async Task Cadence_counts_both_feet_and_months_average_what_their_runs_have()
    {
        var fitness = await FitnessAsync();

        fitness.Runs.Single(r => r.Id == Easy).Cadence.ShouldBe(168);
        fitness.GoodCardiacDrift.ShouldBe(0.05);
        fitness.Months.Select(m => (m.Year, m.Month)).ShouldBe([(2026, 9), (2026, 10)]);
        var september = fitness.Months[0];
        september.Cadence.ShouldBe(166); // Easy 168, Long 164; the others have none
        september.CardiacDrift!.Value.ShouldBe(0.05, 1e-9);
        september.AerobicEfficiency!.Value.ShouldBe((1000.0 / 360 * 60 / 140 + 2.5 * 60 / 143.5 + 2.5 * 60 / 150) / 3, 1e-9);
    }

    [Test]
    public async Task A_runs_details_show_its_fitness_figures_and_cardiac_drift_as_the_weeks_long_run()
    {
        var longRun = await DetailAsync(Long);
        longRun.ShouldNotBeNull();
        (longRun.Cadence, longRun.IsLongRun).ShouldBe((164, true));
        longRun.CardiacDrift!.Value.ShouldBe(0.05, 1e-9);
        longRun.AerobicEfficiency!.Value.ShouldBe(2.5 * 60 / 143.5, 1e-9);

        var easy = await DetailAsync(Easy);
        (easy!.IsLongRun, easy.CardiacDrift).ShouldBe((false, null));

        (await DetailAsync(Treadmill)).ShouldBeNull();
    }

    private async Task<FitnessResponse> FitnessAsync() => (await _browser.GetFromJsonAsync<FitnessResponse>("/api/fitness"))!;

    private async Task<RunFitness?> DetailAsync(long id) => (await _browser.GetFromJsonAsync<RunDetail>($"/api/runs/{id}"))!.Fitness;

    private sealed record FitnessResponse(double GoodCardiacDrift, List<FitnessRun> Runs, List<FitnessMonth> Months);

    private sealed record FitnessRun(long Id, string Name, DateTime StartLocal, double Distance, double? AerobicEfficiency, double? CardiacDrift, double? Cadence);

    private sealed record FitnessMonth(int Year, int Month, double? AerobicEfficiency, double? CardiacDrift, double? Cadence);

    private sealed record RunDetail(long Id, RunFitness? Fitness);

    private sealed record RunFitness(double? AerobicEfficiency, double? Cadence, bool IsLongRun, double? CardiacDrift);
}

/// <summary>Runs stored before get their treadmill flag and their Splits' heart rate and grade-adjusted speed from what is stored.</summary>
public sealed class StoredFitnessDataTests
{
    [Test]
    public async Task Runs_stored_before_get_their_fitness_data_from_the_stored_summary_and_detail()
    {
        var path = Path.Combine(Path.GetTempPath(), $"running-dashboard-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        await using (var db = new AppDbContext(options))
            await db.GetService<IMigrator>().MigrateAsync("20261014071842_StoreRunHeartRateAndCadence");
        await using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO Runs (Id, Name, SportType, StartUtc, StartLocal, Distance, MovingTime, ElapsedTime, ElevationGain, AverageHeartRate,
                                  RawSummaryJson, DetailFetchedAt, RawDetailJson)
                VALUES (1, 'Outdoors', 'Run', '2026-09-08 16:00:00', '2026-09-08 18:00:00', 2000, 720, 720, 0, 140, '{"trainer": false}',
                        '2026-09-08 20:00:00+00:00',
                        '{"splits_metric": [{"split": 1, "average_heartrate": 140, "average_grade_adjusted_speed": 2.5},
                                            {"split": 2, "average_heartrate": 140, "average_grade_adjusted_speed": 3.0}],
                          "splits_standard": [{"split": 1, "average_heartrate": 140, "average_grade_adjusted_speed": 2.7}]}'),
                       (2, 'Indoors', 'Run', '2026-09-09 16:00:00', '2026-09-09 18:00:00', 5000, 1800, 1800, 0, 150, '{"trainer": true}', NULL, NULL);
                INSERT INTO Split (RunId, Unit, "Index", Distance, MovingTime, ElapsedTime, ElevationDifference)
                VALUES (1, 'Kilometre', 1, 1000, 400, 400, 0), (1, 'Kilometre', 2, 1000, 320, 320, 0), (1, 'Mile', 1, 1609, 640, 640, 0);
                """;
            await insert.ExecuteNonQueryAsync();
        }

        var app = new TestApp(path);
        try
        {
            var fitness = (await app.CreateBrowser().GetFromJsonAsync<FitnessResponse>("/api/fitness"))!;

            // (2.5 × 400 + 3.0 × 320) m in 720 s, per minute, over 140 bpm.
            fitness.Runs.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                run => run.Name.ShouldBe("Outdoors"),
                run => run.AerobicEfficiency!.Value.ShouldBe((2.5 * 400 + 3.0 * 320) / 720 * 60 / 140, 1e-9));
        }
        finally
        {
            await app.DisposeAsync();
            File.Delete(path);
            if (Directory.Exists(app.BackupFolder))
                Directory.Delete(app.BackupFolder, recursive: true);
        }
    }

    private sealed record FitnessResponse(List<FitnessRun> Runs);

    private sealed record FitnessRun(string Name, double? AerobicEfficiency);
}
