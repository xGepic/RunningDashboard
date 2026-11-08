using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>The clock stands at Friday 2026-10-09, so the current Training Week starts Monday 2026-10-05.</summary>
public sealed class WeeklyTimeGoalTests
{
    private readonly TestApp _app = new();

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task The_overview_shows_the_goal_once_it_is_set()
    {
        var browser = _app.CreateBrowser();
        (await browser.GetOverviewAsync()).WeeklyTimeGoal.ShouldBeNull();

        var response = await browser.PutAsJsonAsync("/api/weekly-time-goal", new WeeklyTimeGoalRequest(7 * 3600));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<WeeklyTimeGoalResponse>()).ShouldBe(new WeeklyTimeGoalResponse(25200));
        (await browser.GetOverviewAsync()).WeeklyTimeGoal.ShouldBe(new WeeklyTimeGoalResponse(25200));
    }

    [Test]
    public async Task Setting_the_goal_again_replaces_it()
    {
        var browser = _app.CreateBrowser();
        await browser.PutAsJsonAsync("/api/weekly-time-goal", new WeeklyTimeGoalRequest(7 * 3600));
        await browser.PutAsJsonAsync("/api/weekly-time-goal", new WeeklyTimeGoalRequest(5 * 3600 + 1800));

        (await browser.GetOverviewAsync()).WeeklyTimeGoal.ShouldBe(new WeeklyTimeGoalResponse(19800));
    }

    [TestCase(null)]
    [TestCase(0)]
    [TestCase(-60)]
    [TestCase(100 * 3600 + 1)]
    public async Task A_goal_that_is_not_a_sensible_time_is_rejected(int? time)
    {
        var browser = _app.CreateBrowser();

        var response = await browser.PutAsJsonAsync("/api/weekly-time-goal", new WeeklyTimeGoalRequest(time));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        problem!.Errors["time"].ShouldBe(["The goal is a positive number of seconds, at most 100 hours."]);
        (await browser.GetOverviewAsync()).WeeklyTimeGoal.ShouldBeNull();
    }

    [Test]
    public async Task A_removed_goal_is_gone()
    {
        var browser = _app.CreateBrowser();
        await browser.PutAsJsonAsync("/api/weekly-time-goal", new WeeklyTimeGoalRequest(7 * 3600));

        var response = await browser.DeleteAsync("/api/weekly-time-goal");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await browser.GetOverviewAsync()).WeeklyTimeGoal.ShouldBeNull();
    }

    [Test]
    public async Task This_weeks_training_time_counts_its_runs_rides_and_swims()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Last week's run", new DateTime(2026, 10, 4, 18, 0, 0), 10000, 3000),
            new TestActivity(2, "Easy run", new DateTime(2026, 10, 6, 18, 0, 0), 6000, 2100),
            new TestActivity(3, "Zwift Watopia", new DateTime(2026, 10, 7, 19, 0, 0), 30000, 3600, "VirtualRide"),
            new TestActivity(4, "Pool", new DateTime(2026, 10, 8, 7, 0, 0), 2000, 2400, "Swim"));

        (await browser.GetOverviewAsync()).ThisWeek.ShouldBe(new TrainingTimeWeekResponse(new DateOnly(2026, 10, 5), 2100, 3600, 2400));
    }

    [Test]
    public async Task This_weeks_training_time_is_there_without_rides_or_swims_too()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Easy run", new DateTime(2026, 10, 6, 18, 0, 0), 6000, 2100));

        var overview = await browser.GetOverviewAsync();

        overview.TrainingTime.ShouldBeEmpty();
        overview.ThisWeek.ShouldBe(new TrainingTimeWeekResponse(new DateOnly(2026, 10, 5), 2100, 0, 0));
    }

    /// <param name="Time">Seconds.</param>
    private sealed record WeeklyTimeGoalRequest(int? Time);
}
