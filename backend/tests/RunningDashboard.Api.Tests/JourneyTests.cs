using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// The Virtual Journey, today being Fri 9 Oct 2026. Without a Yearly Goal the tour is the capitals tour: from Vienna,
/// as the crow flies, Bratislava is about 55 km on and Budapest about 216 km, the whole tour about 2,062 km. With a
/// goal, the tour is the longest that fits within it.
/// </summary>
public sealed class JourneyTests
{
    private readonly TestApp _app = new();

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task The_years_runs_in_order_reach_each_stop_on_the_date_of_the_run_that_brings_the_distance_up_to_it()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Last year", new DateTime(2025, 12, 31, 9, 0, 0), 100000, 36000),
            // 2025-12-31T23:30Z in UTC, but 2026 where it was run.
            new TestActivity(2, "New Year's run", new DateTime(2026, 1, 1, 0, 30, 0), 30000, 10800, UtcOffsetHours: 1),
            new TestActivity(3, "Danube Ride", new DateTime(2026, 1, 20, 10, 0, 0), 80000, 10800, "Ride"),
            new TestActivity(4, "Into Bratislava", new DateTime(2026, 2, 1, 9, 0, 0), 40000, 14400),
            new TestActivity(5, "Long one", new DateTime(2026, 3, 1, 9, 0, 0), 200000, 72000));

        var journey = (await GetJourneyAsync(browser)).Journey;

        journey.Year.ShouldBe(2026);
        journey.Distance.ShouldBe(270000);
        journey.Length.ShouldBe(2_062_000, tolerance: 1000);
        // The current year without a Yearly Goal follows the capitals tour.
        (journey.YearlyGoal, journey.Finished).ShouldBe((null, false));
        journey.Stops.Select(s => s.Name).ShouldBe(
            ["Vienna", "Bratislava", "Budapest", "Zagreb", "Ljubljana", "Vaduz", "Bern", "Prague", "Vienna"]);

        var start = journey.Stops[0];
        (start.At, start.Reached).ShouldBe((0, null));
        journey.Stops[1].At.ShouldBe(55_000, tolerance: 1000);
        journey.Stops[1].Reached.ShouldBe(new ArrivalResponse(new DateOnly(2026, 2, 1), 4, "Into Bratislava"));
        journey.Stops[2].Reached.ShouldBe(new ArrivalResponse(new DateOnly(2026, 3, 1), 5, "Long one"));
        journey.Stops.Skip(3).ShouldAllBe(s => s.Reached == null);
        journey.Stops[^1].At.ShouldBe(journey.Length);
    }

    [Test]
    public async Task One_run_can_reach_several_stops_and_past_the_end_the_tour_is_complete()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Ultra", new DateTime(2026, 5, 2, 6, 0, 0), 2_100_000, 700_000));

        var journey = (await GetJourneyAsync(browser)).Journey;

        journey.Distance.ShouldBeGreaterThan(journey.Length);
        journey.Stops.Skip(1).ShouldAllBe(s => s.Reached == new ArrivalResponse(new DateOnly(2026, 5, 2), 1, "Ultra"));
    }

    [Test]
    public async Task With_a_yearly_goal_the_tour_is_the_longest_that_fits_within_it_at_real_distances()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Into Bratislava", new DateTime(2026, 1, 10, 9, 0, 0), 56000, 20000));

        await SetYearlyGoalAsync(browser, 1_600_000);
        var journey = (await GetJourneyAsync(browser)).Journey;

        journey.YearlyGoal.ShouldBe(1_600_000);
        journey.Length.ShouldBe(1_590_000, tolerance: 1000);
        journey.Stops.Select(s => s.Name).ShouldBe(
            ["Vienna", "Bratislava", "Budapest", "Zagreb", "Ljubljana", "Venice", "Verona", "Bolzano", "Innsbruck", "Munich", "Salzburg", "Vienna"]);
        // Bratislava stays 55 km from Vienna, whatever the goal.
        journey.Stops[1].At.ShouldBe(55_000, tolerance: 1000);
        journey.Stops[1].Reached!.RunName.ShouldBe("Into Bratislava");

        await SetYearlyGoalAsync(browser, 500_000);
        journey = (await GetJourneyAsync(browser)).Journey;

        journey.Length.ShouldBe(499_000, tolerance: 1000);
        journey.Stops.Select(s => s.Name).ShouldBe(["Vienna", "Linz", "České Budějovice", "Brno", "Vienna"]);
    }

    [Test]
    public async Task A_yearly_goal_shorter_than_every_tour_gets_the_shortest()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Run", new DateTime(2026, 6, 1, 9, 0, 0), 10000, 3600));
        await SetYearlyGoalAsync(browser, 100_000);

        var journey = (await GetJourneyAsync(browser)).Journey;

        journey.Length.ShouldBe(194_000, tolerance: 1000);
        journey.Stops.Select(s => s.Name).ShouldBe(["Vienna", "Bratislava", "Sopron", "Wiener Neustadt", "Vienna"]);
    }

    [Test]
    public async Task Every_yearly_goal_from_200_to_3000_km_gets_a_tour_close_to_it()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Run", new DateTime(2026, 6, 1, 9, 0, 0), 10000, 3600));

        for (var goal = 200_000; goal <= 3_000_000; goal += 100_000)
        {
            await SetYearlyGoalAsync(browser, goal);
            var journey = (await GetJourneyAsync(browser)).Journey;

            journey.Stops[0].Name.ShouldBe("Vienna");
            journey.Stops[^1].Name.ShouldBe("Vienna");
            journey.Length.ShouldBeLessThanOrEqualTo(goal);
            // A few long tours are further apart.
            journey.Length.ShouldBeGreaterThan(goal - (goal <= 2_400_000 ? 220_000 : 330_000), $"goal {goal / 1000} km");
        }
    }

    [Test]
    public async Task Reaching_the_yearly_goal_completes_the_tour()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Goal reached", new DateTime(2026, 10, 1, 9, 0, 0), 500_000, 180_000));
        await SetYearlyGoalAsync(browser, 500_000);

        var journey = (await GetJourneyAsync(browser)).Journey;

        journey.Stops[^1].Reached!.RunName.ShouldBe("Goal reached");
    }

    [Test]
    public async Task An_earlier_year_has_its_own_journey_and_the_years_are_those_with_runs_and_the_current_one()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Old", new DateTime(2024, 6, 1, 9, 0, 0), 60000, 21600),
            new TestActivity(2, "Last year", new DateTime(2025, 6, 1, 9, 0, 0), 10000, 3600));
        await SetYearlyGoalAsync(browser, 500_000);

        var page = (await browser.GetFromJsonAsync<JourneyPageResponse>("/api/journey?year=2024"))!;

        page.Journey.Year.ShouldBe(2024);
        page.Journey.Distance.ShouldBe(60000);
        // This year's goal is not 2024's.
        page.Journey.YearlyGoal.ShouldBeNull();
        page.Journey.Finished.ShouldBeTrue();
        page.Journey.Stops[1].Reached!.RunName.ShouldBe("Old");
        page.Years.ShouldBe([2026, 2025, 2024]);
    }

    [Test]
    public async Task A_finished_year_without_a_goal_gets_the_longest_tour_its_distance_completed()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Spring", new DateTime(2025, 4, 1, 9, 0, 0), 500_000, 180_000),
            new TestActivity(2, "Autumn", new DateTime(2025, 10, 1, 9, 0, 0), 527_000, 190_000));

        var journey = (await browser.GetFromJsonAsync<JourneyPageResponse>("/api/journey?year=2025"))!.Journey;

        (journey.YearlyGoal, journey.Finished).ShouldBe((null, true));
        // 1,027 km completes the 958 km tour but not the 1,086 km one.
        journey.Length.ShouldBe(958_000, tolerance: 1000);
        journey.Stops[^1].Reached!.RunName.ShouldBe("Autumn");
    }

    [Test]
    public async Task A_finished_year_keeps_the_tour_picked_for_its_goal_after_the_next_years_goal_is_set()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Long year", new DateTime(2026, 6, 1, 9, 0, 0), 1_700_000, 600_000));
        await SetYearlyGoalAsync(browser, 1_600_000);
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 1, 2, 8, 0, 0, TimeSpan.Zero));
        await SetYearlyGoalAsync(browser, 2_000_000);

        var journey = (await browser.GetFromJsonAsync<JourneyPageResponse>("/api/journey?year=2026"))!.Journey;

        (journey.YearlyGoal, journey.Finished).ShouldBe((1_600_000, true));
        // Not the 1,680 km tour its 1,700 km would have completed.
        journey.Length.ShouldBe(1_590_000, tolerance: 1000);
        (await GetJourneyAsync(browser)).Journey.YearlyGoal.ShouldBe(2_000_000);
    }

    [Test]
    public async Task A_year_out_of_range_is_refused()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Run", new DateTime(2026, 6, 1, 9, 0, 0), 10000, 3600));

        (await browser.GetAsync("/api/journey?year=1800")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task The_overview_has_the_current_years_journey()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Last year", new DateTime(2025, 6, 1, 9, 0, 0), 10000, 3600),
            new TestActivity(2, "This year", new DateTime(2026, 6, 1, 9, 0, 0), 60000, 21600));

        var overview = (await browser.GetFromJsonAsync<OverviewResponse>("/api/overview"))!;

        overview.Journey.Year.ShouldBe(2026);
        overview.Journey.Distance.ShouldBe(60000);
        overview.Journey.Stops[1].Reached!.RunName.ShouldBe("This year");
    }

    private static async Task SetYearlyGoalAsync(HttpClient browser, double distance) =>
        (await browser.PutAsJsonAsync("/api/yearly-goal", new { distance })).EnsureSuccessStatusCode();

    private static async Task<JourneyPageResponse> GetJourneyAsync(HttpClient browser) =>
        (await browser.GetFromJsonAsync<JourneyPageResponse>("/api/journey"))!;

    private sealed record OverviewResponse(JourneyResponse Journey);

    private sealed record JourneyPageResponse(JourneyResponse Journey, List<int> Years);

    private sealed record JourneyResponse(int Year, double Distance, double Length, double? YearlyGoal, bool Finished, List<StopResponse> Stops);

    private sealed record StopResponse(string Name, string Country, double Latitude, double Longitude, double At, ArrivalResponse? Reached);

    private sealed record ArrivalResponse(DateOnly Date, long RunId, string RunName);
}
