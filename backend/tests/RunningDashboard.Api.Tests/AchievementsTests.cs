using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Achievements earned with five Runs, today being Fri 9 Oct 2026 (Training Week from Mon 5 Oct).
///
/// | Run        | Date           | Distance  | Best Efforts      |
/// |------------|----------------|-----------|-------------------|
/// | Short Half | Mon 2025-03-03 | 21,050 m  | 10K               |
/// | Half Race  | Sun 2025-03-09 | 21,150 m  | 10K, Half         |
/// | Long Run   | Wed 2025-03-12 | 25,000 m  |                   |
/// | Big Run    | Fri 2025-03-14 | 35,000 m  | 10K, Half, 30K    |
/// | Easy Run   | Tue 2026-10-06 | 12,000 m  |                   |
///
/// Short Half's GPS came up short of a half marathon, so it has no Best Effort over it. Also a 300 km Ride this week,
/// which counts toward nothing.
/// </summary>
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public sealed class AchievementsTests
{
    private const long ShortHalf = 1;
    private const long HalfRace = 2;
    private const long LongRun = 3;
    private const long BigRun = 4;
    private const long EasyRun = 5;

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync() =>
        _browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(ShortHalf, "Short Half", new DateTime(2025, 3, 3, 18, 0, 0), 21050, 7000, BestEfforts: [new("10K", 10000, 3200)]),
            new TestActivity(HalfRace, "Half Race", new DateTime(2025, 3, 9, 9, 0, 0), 21150, 6800,
                BestEfforts: [new("10K", 10000, 3100), new("Half-Marathon", 21097, 6780)]),
            new TestActivity(LongRun, "Long Run", new DateTime(2025, 3, 12, 18, 0, 0), 25000, 9000),
            new TestActivity(BigRun, "Big Run", new DateTime(2025, 3, 14, 7, 0, 0), 35000, 12600,
                BestEfforts: [new("10K", 10000, 3500), new("Half-Marathon", 21097, 7500), new("30K", 30000, 10800)]),
            new TestActivity(EasyRun, "Easy Run", new DateTime(2026, 10, 6, 18, 0, 0), 12000, 4200),
            new TestActivity(20, "Alpine Ride", new DateTime(2026, 10, 7, 7, 0, 0), 300000, 36000, "Ride"));

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task A_single_run_achievement_is_earned_once_by_the_first_run_with_a_best_effort_over_its_distance()
    {
        var singleRun = await GroupAsync("singleRun");

        singleRun.Select(a => (a.Name, a.Distance, a.Repeatable)).ShouldBe(
            [("10K", 10000, false), ("Half-Marathon", 21097, false), ("30K", 30000, false), ("Marathon", 42195, false), ("50K", 50000, false)]);
        singleRun.Select(a => a.Earned.Select(e => e.RunId)).ShouldBe([[ShortHalf], [HalfRace], [BigRun], [], []]);
        singleRun[1].Earned.ShouldBe([new Earned(HalfRace, "Half Race", new DateTime(2025, 3, 9, 9, 0, 0))]);
    }

    [Test]
    public async Task A_lifetime_achievement_is_earned_by_the_run_that_takes_all_runs_past_it_and_rides_count_for_nothing()
    {
        var lifetime = await GroupAsync("lifetime");

        lifetime.Select(a => a.Name).ShouldBe(
            ["100 km", "250 km", "500 km", "1,000 km", "2,000 km", "3,000 km", "5,000 km", "7,500 km", "10,000 km"]);
        lifetime.ShouldAllBe(a => !a.Repeatable);
        // 21.05 + 21.15 + 25 = 67.2 km before the Big Run, 102.2 km after it.
        lifetime[0].Earned.ShouldBe([new Earned(BigRun, "Big Run", new DateTime(2025, 3, 14, 7, 0, 0))]);
        lifetime.Skip(1).ShouldAllBe(a => a.Earned.Count == 0);
    }

    [Test]
    public async Task Every_training_week_month_and_year_earns_each_threshold_it_passes_again()
    {
        var week = await GroupAsync("week");
        var month = await GroupAsync("month");
        var year = await GroupAsync("year");

        week.Select(a => a.Name).ShouldBe(["40 km week", "60 km week", "80 km week", "100 km week"]);
        month.Select(a => a.Name).ShouldBe(["100 km month", "150 km month", "200 km month", "250 km month"]);
        year.Select(a => a.Name).ShouldBe(["500 km year", "1,000 km year", "1,500 km year", "2,000 km year"]);
        new[] { week, month, year }.SelectMany(g => g).ShouldAllBe(a => a.Repeatable);

        // Week of 3 Mar 2025: 21.05 + 21.15 = 42.2 km; week of 10 Mar: 25 + 35 = 60 km.
        week.Select(a => a.Earned.Select(e => e.RunId)).ShouldBe([[HalfRace, BigRun], [BigRun], [], []]);
        // March 2025: 102.2 km.
        month.Select(a => a.Earned.Select(e => e.RunId)).ShouldBe([[BigRun], [], [], []]);
        year.ShouldAllBe(a => a.Earned.Count == 0);
    }

    [Test]
    public async Task Only_the_next_locked_achievement_of_each_group_shows_progress_toward_it()
    {
        // The longest Run; all Runs; and this year's, this month's and this week's Runs: the Easy Run, without the Ride.
        (await ProgressAsync("singleRun")).ShouldBe([null, null, null, 35000, null]);
        (await ProgressAsync("lifetime")).ShouldBe([null, 114200, null, null, null, null, null, null, null]);
        (await ProgressAsync("year")).ShouldBe([12000, null, null, null]);
        (await ProgressAsync("month")).ShouldBe([null, 12000, null, null]);
        (await ProgressAsync("week")).ShouldBe([null, null, 12000, null]);
    }

    [Test]
    public async Task Runs_add_up_in_the_order_they_were_run_even_across_time_zones()
    {
        // At 10:00 in New York (14:00 UTC) after 12:00 in Vienna (10:00 UTC), although its local time is earlier.
        await using var app = new TestApp();
        var browser = await app.ConnectWithActivitiesAsync(
            new TestActivity(1, "New York Run", new DateTime(2026, 5, 3, 10, 0, 0), 60000, 21600, UtcOffsetHours: -4),
            new TestActivity(2, "Vienna Run", new DateTime(2026, 5, 3, 12, 0, 0), 50000, 18000, UtcOffsetHours: 2));

        var achievements = (await browser.GetFromJsonAsync<List<Achievement>>("/api/achievements"))!;

        achievements.Single(a => a.Id == "lifetime-100000").Earned.Select(e => e.RunName).ShouldBe(["New York Run"]);
    }

    private async Task<List<double?>> ProgressAsync(string group) => [.. (await GroupAsync(group)).Select(a => a.Progress)];

    private async Task<List<Achievement>> GroupAsync(string group) =>
        [.. (await _browser.GetFromJsonAsync<List<Achievement>>("/api/achievements"))!.Where(a => a.Group == group)];

    private sealed record Achievement(string Id, string Group, string Name, double Distance, bool Repeatable, List<Earned> Earned, double? Progress);

    private sealed record Earned(long RunId, string RunName, DateTime Date);
}
