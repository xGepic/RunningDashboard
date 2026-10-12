using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>The clock stands at Friday 2026-10-09, so the Yearly Goal is for 2026.</summary>
public sealed class YearlyGoalTests : IAsyncLifetime
{
    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    public Task InitializeAsync()
    {
        _browser = _app.CreateBrowser();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task The_overview_shows_the_goal_set_for_the_current_year()
    {
        Assert.Null((await GetOverviewAsync()).YearlyGoal);

        var response = await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(2_000_000));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new YearlyGoalResponse(2026, 2_000_000), await response.Content.ReadFromJsonAsync<YearlyGoalResponse>());
        Assert.Equal(new YearlyGoalResponse(2026, 2_000_000), (await GetOverviewAsync()).YearlyGoal);
    }

    [Fact]
    public async Task Setting_the_goal_again_replaces_it()
    {
        await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(2_000_000));
        await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(1_800_000));

        Assert.Equal(new YearlyGoalResponse(2026, 1_800_000), (await GetOverviewAsync()).YearlyGoal);
    }

    [Fact]
    public async Task Last_years_goal_no_longer_counts_and_a_new_one_is_for_the_new_year()
    {
        await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(2_000_000));

        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 1, 1, 8, 0, 0, TimeSpan.Zero));

        Assert.Null((await GetOverviewAsync()).YearlyGoal);
        await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(2_500_000));
        Assert.Equal(new YearlyGoalResponse(2027, 2_500_000), (await GetOverviewAsync()).YearlyGoal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-5000.0)]
    [InlineData(20_000_001.0)]
    public async Task A_goal_that_is_not_a_sensible_distance_is_rejected(double? distance)
    {
        var response = await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(distance));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        Assert.Equal(["The goal is a positive number of metres, at most 20000 km."], problem!.Errors["distance"]);
        Assert.Null((await GetOverviewAsync()).YearlyGoal);
    }

    [Fact]
    public async Task A_removed_goal_is_gone()
    {
        await _browser.PutAsJsonAsync("/api/yearly-goal", new YearlyGoalRequest(2_000_000));

        var response = await _browser.DeleteAsync("/api/yearly-goal");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null((await GetOverviewAsync()).YearlyGoal);
    }

    private async Task<OverviewResponse> GetOverviewAsync() =>
        (await _browser.GetFromJsonAsync<OverviewResponse>("/api/overview"))!;

    private sealed record OverviewResponse(YearlyGoalResponse? YearlyGoal);

    /// <param name="Distance">Metres.</param>
    private sealed record YearlyGoalRequest(double? Distance);

    /// <param name="Distance">Metres.</param>
    private sealed record YearlyGoalResponse(int Year, double Distance);

    private sealed record ValidationProblem(string Title, Dictionary<string, string[]> Errors);
}
