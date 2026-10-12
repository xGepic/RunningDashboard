using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// The Race Result of a Goal Race comes from the longest Run on race day (ADR 0003): its Best Effort over the race
/// distance, otherwise its elapsed time, otherwise there is none.
/// </summary>
public sealed class RaceResultTests
{
    private const double Marathon = 42195;

    /// <summary>The first marathon, Sun 27 Sep 2026: the watch measured 42.58 km.</summary>
    private static readonly TestActivity Finisher = new(
        20351282630, "Finisher.", new DateTime(2026, 9, 27, 9, 47, 0), 42582.8, 18043, ElapsedTime: 18190,
        BestEfforts: [new("Half-Marathon", 21097, 8650), new("30K", 30000, 12480), new("Marathon", Marathon, 18011)]);

    private readonly TestApp _app = new();


    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task A_marathon_run_measured_longer_gives_the_marathon_best_effort()
    {
        var browser = await _app.ConnectWithActivitiesAsync(Finisher);

        var race = await CreateAsync(browser, new GoalRaceRequest("Vienna Marathon", new DateOnly(2026, 9, 27), Marathon, 5 * 3600, 14));

        // 5:00:11 over the marathon, 11 s over the Target Time; the whole Run took 5:03:10.
        race.RaceResult.ShouldBe(new RaceResultResponse(Finisher.Id, "Finisher.", 18011, "bestEffort", 11));
        (await ListAsync(browser)).ShouldHaveSingleItem().ShouldBe(race);
    }

    [Test]
    public async Task With_several_runs_on_race_day_the_longest_one_is_the_race()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Warm-up", new DateTime(2026, 9, 27, 9, 10, 0), 2100, 780, BestEfforts: [new("1K", 1000, 360)]),
            Finisher,
            new TestActivity(2, "Shake-out", new DateTime(2026, 9, 27, 17, 0, 0), 3000, 1200, BestEfforts: [new("1K", 1000, 390)]));

        var race = await CreateAsync(browser, new GoalRaceRequest("Vienna Marathon", new DateOnly(2026, 9, 27), Marathon, null, 14));

        race.RaceResult.ShouldBe(new RaceResultResponse(Finisher.Id, "Finisher.", 18011, "bestEffort", null));
    }

    [Test]
    public async Task Without_a_run_on_race_day_there_is_no_race_result()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Saturday shake-out", new DateTime(2026, 10, 3, 9, 0, 0), 5000, 1800, BestEfforts: [new("5K", 5000, 1800)]));

        var race = await CreateAsync(browser, new GoalRaceRequest("Autumn 10K", new DateOnly(2026, 10, 4), 10000, 3000, 4));

        race.RaceResult.ShouldBeNull();
    }

    [Test]
    public async Task A_custom_distance_without_a_best_effort_falls_back_to_the_elapsed_time()
    {
        var trailRace = new TestActivity(
            1, "Wienerwald trail race", new DateTime(2026, 9, 20, 10, 0, 0), 15730, 5940, "TrailRun", ElapsedTime: 6100,
            BestEfforts: [new("10K", 10000, 3790), new("15K", 15000, 5810)]);
        var browser = await _app.ConnectWithActivitiesAsync(trailRace);

        var race = await CreateAsync(browser, new GoalRaceRequest("Wienerwald trail", new DateOnly(2026, 9, 20), 15500, null, 2));

        race.RaceResult.ShouldBe(new RaceResultResponse(1, "Wienerwald trail race", 6100, "elapsedTime", null));
    }

    [Test]
    public async Task The_official_half_marathon_distance_matches_stravas_half_marathon_best_effort()
    {
        // Strava's half marathon is 21,097 m; the Goal Race has the official 21,097.5 m.
        var halfMarathon = new TestActivity(
            1, "Half marathon", new DateTime(2026, 4, 12, 9, 0, 0), 21150, 6640,
            BestEfforts: [new("20K", 20000, 6290), new("Half-Marathon", 21097, 6620)]);
        var browser = await _app.ConnectWithActivitiesAsync(halfMarathon);

        var race = await CreateAsync(browser, new GoalRaceRequest("Vienna Half", new DateOnly(2026, 4, 12), 21097.5, 6900, 8));

        // 1:50:20 against a Target Time of 1:55:00.
        race.RaceResult.ShouldBe(new RaceResultResponse(1, "Half marathon", 6620, "bestEffort", -280));
    }

    [Test]
    public async Task While_the_race_runs_best_efforts_are_still_loading_the_race_result_is_pending()
    {
        _app.Strava.RespondWithActivities("/api/v3/athlete/activities", Finisher);
        _app.Strava.RespondWithFixture(HttpMethod.Get, "/api/v3/athlete/activities", "activities-empty.json");
        // Strava has not answered with the marathon's detail yet.
        _app.Strava.RespondWithFixtureAfter(new TaskCompletionSource().Task, HttpMethod.Get, $"/api/v3/activities/{Finisher.Id}", "activities-empty.json");
        var browser = _app.CreateBrowser();
        await _app.ConnectStravaAsync(browser);
        await TestApp.WaitForStatusAsync(browser, s => s.Phase == "importing-details");

        var race = await CreateAsync(browser, new GoalRaceRequest("Vienna Marathon", new DateOnly(2026, 9, 27), Marathon, 5 * 3600, 14));

        // Not the 5:03:10 the whole Run took: the Marathon Best Effort is still to come.
        race.RaceResult.ShouldBe(new RaceResultResponse(Finisher.Id, "Finisher.", null, "pending", null));
    }

    private static async Task<GoalRaceResponse> CreateAsync(HttpClient browser, GoalRaceRequest race)
    {
        var response = await browser.PostAsJsonAsync("/api/goal-races", race);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GoalRaceResponse>())!;
    }

    private static async Task<List<GoalRaceResponse>> ListAsync(HttpClient browser) =>
        (await browser.GetFromJsonAsync<List<GoalRaceResponse>>("/api/goal-races"))!;

    /// <param name="TargetTime">Seconds.</param>
    private sealed record GoalRaceRequest(string Name, DateOnly Date, double Distance, int? TargetTime, int TrainingBlockWeeks);

    private sealed record GoalRaceResponse(int Id, string Name, int? TargetTime, RaceResultResponse? RaceResult);

    /// <param name="ElapsedTime">Seconds.</param>
    /// <param name="Source">"bestEffort", "elapsedTime" or "pending".</param>
    /// <param name="DifferenceFromTarget">Seconds over (positive) or under (negative) the Target Time.</param>
    private sealed record RaceResultResponse(long RunId, string RunName, int? ElapsedTime, string Source, int? DifferenceFromTarget);
}
