using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>The clock stands at Friday 2026-10-09, so the Yearly Goal is for 2026.</summary>
public sealed class YearlyGoalTests
{
    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    [SetUp]
    public void SetUp()
    {
        _browser = _app.CreateBrowser();
    }

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task The_overview_shows_the_goal_set_for_the_current_year()
    {
        (await _browser.GetOverviewAsync()).YearlyGoal.ShouldBeNull();

        var response = await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(2_000_000));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<YearlyGoalResponse>()).ShouldBe(new YearlyGoalResponse(2026, 2_000_000));
        (await _browser.GetOverviewAsync()).YearlyGoal.ShouldBe(new YearlyGoalResponse(2026, 2_000_000));
    }

    [Test]
    public async Task Setting_the_goal_again_replaces_it()
    {
        await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(2_000_000));
        await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(1_800_000));

        (await _browser.GetOverviewAsync()).YearlyGoal.ShouldBe(new YearlyGoalResponse(2026, 1_800_000));
    }

    [Test]
    public async Task Last_years_goal_no_longer_counts_and_a_new_one_is_for_the_new_year()
    {
        await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(2_000_000));

        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 1, 1, 8, 0, 0, TimeSpan.Zero));

        (await _browser.GetOverviewAsync()).YearlyGoal.ShouldBeNull();
        await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(2_500_000));
        (await _browser.GetOverviewAsync()).YearlyGoal.ShouldBe(new YearlyGoalResponse(2027, 2_500_000));
    }

    [TestCase(null)]
    [TestCase(0.0)]
    [TestCase(-5000.0)]
    [TestCase(20_000_001.0)]
    public async Task A_goal_that_is_not_a_sensible_distance_is_rejected(double? distance)
    {
        var response = await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(distance));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        problem!.Errors["distance"].ShouldBe(["The goal is a positive number of metres, at most 20000 km."]);
        (await _browser.GetOverviewAsync()).YearlyGoal.ShouldBeNull();
    }

    [Test]
    public async Task A_removed_goal_is_gone()
    {
        await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(2_000_000));

        var response = await _browser.DeleteAsync("/api/yearly-goal");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _browser.GetOverviewAsync()).YearlyGoal.ShouldBeNull();
    }

    /// <param name="Distance">Metres.</param>
    private sealed record YearlyGoalRequest(double? Distance);
}
