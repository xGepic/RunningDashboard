using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// A year's highlights, today being Fri 9 Oct 2026. Training Weeks across New Year count only with their days inside
/// the year.
///
/// | Run          | Date             | Distance | Moving Time | Shoe     | Best Efforts             |
/// |--------------|------------------|----------|-------------|----------|--------------------------|
/// | First Run    | Tue 2024-12-10   | 10,000 m |   3,600 s   |          | 5K 1,560 s               |
/// | Silvesterlauf| Tue 2024-12-31   | 12,000 m |   4,320 s   |          |                          |
/// | New Year Run | Thu 2025-01-02   | 12,000 m |   4,200 s   | Pegasus  |                          |
/// | Tuesday Run  | Tue 2025-01-07   | 15,000 m |   5,400 s   | Adizero  |                          |
/// | Sunday Run   | Sun 2025-01-12   | 16,000 m |   5,700 s   | Adizero  | 5K 1,500 s               |
/// | Half Race    | Sat 2025-03-15   | 30,000 m |  10,800 s   | Pegasus  | 5K 1,520 s, Half 7,000 s |
/// | Last Run     | Tue 2025-12-30   |  5,000 m |   1,800 s   |          |                          |
/// | Spring Run   | Sun 2026-02-01   | 10,000 m |   3,500 s   |          |                          |
///
/// Silvesterlauf and New Year Run share a Training Week (Mon 30 Dec 2024 to Sun 5 Jan 2025); so do Last Run and the
/// first days of 2026. The Half Race is the Goal Race "Spring Half" on 15 Mar 2025, Target Time 2:00:00. Also a Ride
/// and a Swim in 2025, and a Swim in 2024.
/// </summary>
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public sealed class YearReviewTests
{
    private const string Pegasus = "g31444377";
    private const string Adizero = "g33395946";
    private const long FirstRun = 1;
    private const long SundayRun = 5;
    private const long HalfRace = 6;

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;
    private int _goalRaceId;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, "/api/v3/athlete", "athlete.json");
        _browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(FirstRun, "First Run", new DateTime(2024, 12, 10, 18, 0, 0), 10000, 3600, BestEfforts: [new("5K", 5000, 1560)]),
            new TestActivity(2, "Silvesterlauf", new DateTime(2024, 12, 31, 14, 0, 0), 12000, 4320),
            new TestActivity(20, "Old Swim", new DateTime(2024, 7, 1, 12, 0, 0), 1000, 1500, "Swim"),
            new TestActivity(3, "New Year Run", new DateTime(2025, 1, 2, 10, 0, 0), 12000, 4200, GearId: Pegasus),
            new TestActivity(4, "Tuesday Run", new DateTime(2025, 1, 7, 18, 0, 0), 15000, 5400, GearId: Adizero),
            new TestActivity(SundayRun, "Sunday Run", new DateTime(2025, 1, 12, 9, 0, 0), 16000, 5700, GearId: Adizero,
                BestEfforts: [new("5K", 5000, 1500)]),
            new TestActivity(HalfRace, "Half Race", new DateTime(2025, 3, 15, 9, 0, 0), 30000, 10800, GearId: Pegasus,
                BestEfforts: [new("5K", 5000, 1520), new("Half-Marathon", 21097, 7000)]),
            new TestActivity(21, "Danube Ride", new DateTime(2025, 5, 1, 10, 0, 0), 50000, 7200, "Ride"),
            new TestActivity(22, "Lake Swim", new DateTime(2025, 6, 1, 10, 0, 0), 1500, 2000, "Swim"),
            new TestActivity(7, "Last Run", new DateTime(2025, 12, 30, 8, 0, 0), 5000, 1800),
            new TestActivity(8, "Spring Run", new DateTime(2026, 2, 1, 9, 0, 0), 10000, 3500));

        _goalRaceId = (await _browser.CreateGoalRaceAsync(new GoalRaceRequest("Spring Half", new DateOnly(2025, 3, 15), 21097, 7200, 12))).Id;
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task A_finished_year_is_compared_with_the_whole_year_before()
    {
        var review = await ReviewAsync(2025);

        (review.Year, review.From, review.Until).ShouldBe((2025, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));
        review.Totals.ShouldBe(new Totals(5, 78000, 27900, 0));
        review.PreviousYear.ShouldBe(new Totals(2, 22000, 7920, 0));
    }

    [Test]
    public async Task The_longest_run_biggest_week_and_biggest_month_count_only_the_years_days()
    {
        var review = await ReviewAsync(2025);

        review.LongestRun.ShouldBe(new ReviewRun(HalfRace, "Half Race", new DateTime(2025, 3, 15, 9, 0, 0), 30000, 10800));
        review.BiggestWeek.ShouldBe(new Week(new DateOnly(2025, 1, 6), new DateOnly(2025, 1, 12), 2, 31000));
        review.BiggestMonth.ShouldBe(new BiggestMonthOf(1, 3, 43000));
    }

    [Test]
    public async Task The_longest_streak_within_the_year_starts_on_1_january_when_its_first_week_began_the_year_before()
    {
        (await ReviewAsync(2025)).LongestStreak.ShouldBe(new Streak(2, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 12)));
    }

    [Test]
    public async Task Personal_bests_are_the_years_fastest_best_efforts_that_were_one_when_run_with_the_one_before()
    {
        (await ReviewAsync(2025)).PersonalBests.ShouldBe([
            new PersonalBest("5K", 5000, 1500, 1560, SundayRun, "Sunday Run", new DateTime(2025, 1, 12, 9, 0, 0)),
            new PersonalBest("Half-Marathon", 21097, 7000, null, HalfRace, "Half Race", new DateTime(2025, 3, 15, 9, 0, 0)),
        ]);
    }

    [Test]
    public async Task The_years_goal_races_its_most_used_shoe_and_how_many_rides_and_swims()
    {
        var review = await ReviewAsync(2025);

        review.GoalRaces.ShouldBe([new GoalRace(_goalRaceId, "Spring Half", new DateOnly(2025, 3, 15), 21097, 7200, new RaceResult(HalfRace, 7000, -200))]);
        review.MostUsedShoe.ShouldBe(new Shoe("Nike Pegasus 41", 2, 42000));
        (review.Rides, review.Swims).ShouldBe((1, 1));
    }

    [Test]
    public async Task The_current_year_so_far_is_compared_with_the_same_time_last_year_and_is_the_default()
    {
        var review = (await _browser.GetFromJsonAsync<YearReview>("/api/review"))!;

        (review.Year, review.From, review.Until).ShouldBe((2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 9)));
        review.Totals.ShouldBe(new Totals(1, 10000, 3500, 0));
        // Up to 9 Oct 2025: without the Last Run on 30 Dec.
        review.PreviousYear.ShouldBe(new Totals(4, 73000, 26100, 0));
    }

    [Test]
    public async Task The_first_year_starts_with_the_first_run_and_has_nothing_to_compare_with()
    {
        var review = await ReviewAsync(2024);

        review.From.ShouldBe(new DateOnly(2024, 12, 10));
        review.PreviousYear.ShouldBeNull();
        review.PersonalBests.ShouldBe([new PersonalBest("5K", 5000, 1560, null, FirstRun, "First Run", new DateTime(2024, 12, 10, 18, 0, 0))]);
        review.BiggestWeek.ShouldBe(new Week(new DateOnly(2024, 12, 30), new DateOnly(2024, 12, 31), 1, 12000));
        (review.Rides, review.Swims).ShouldBe((0, 1));
    }

    [Test]
    public async Task A_year_without_runs_has_no_highlights()
    {
        var review = await ReviewAsync(2023);

        review.Totals.ShouldBe(new Totals(0, 0, 0, 0));
        (review.LongestRun, review.BiggestWeek, review.BiggestMonth, review.LongestStreak, review.MostUsedShoe).ShouldBe((null, null, null, null, null));
        review.PersonalBests.ShouldBeEmpty();
        review.GoalRaces.ShouldBeEmpty();
    }

    [Test]
    public async Task A_year_out_of_range_is_refused()
    {
        (await _browser.GetAsync("/api/review?year=1800")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<YearReview> ReviewAsync(int year) => (await _browser.GetFromJsonAsync<YearReview>($"/api/review?year={year}"))!;

    private sealed record YearReview(
        int Year,
        DateOnly From,
        DateOnly Until,
        Totals Totals,
        Totals? PreviousYear,
        ReviewRun? LongestRun,
        Week? BiggestWeek,
        BiggestMonthOf? BiggestMonth,
        List<PersonalBest> PersonalBests,
        Streak? LongestStreak,
        List<GoalRace> GoalRaces,
        Shoe? MostUsedShoe,
        int Rides,
        int Swims);

    private sealed record Totals(int Runs, double Distance, int MovingTime, double ElevationGain);

    private sealed record ReviewRun(long Id, string Name, DateTime StartLocal, double Distance, int MovingTime);

    private sealed record Week(DateOnly Start, DateOnly End, int Runs, double Distance);

    private sealed record BiggestMonthOf(int Month, int Runs, double Distance);

    private sealed record PersonalBest(string Name, double Distance, int ElapsedTime, int? PreviousElapsedTime, long RunId, string RunName, DateTime StartLocal);

    private sealed record Streak(int Weeks, DateOnly Start, DateOnly End);

    private sealed record GoalRace(int Id, string Name, DateOnly Date, double Distance, int? TargetTime, RaceResult? RaceResult);

    private sealed record RaceResult(long RunId, int? ElapsedTime, int? DifferenceFromTarget);

    private sealed record Shoe(string Name, int Runs, double Distance);
}
