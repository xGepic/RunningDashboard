using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// The Training page between Training Blocks: a countdown to the next one with recent weekly distance, or an empty
/// state without an upcoming Goal Race. The clock stands at Friday 9 Oct 2026 unless a test moves it.
/// </summary>
public sealed class TrainingCountdownTests : IAsyncLifetime
{
    private const double Marathon = 42195;

    /// <summary>Sun 27 Sep 2026, 14 weeks: Mon 22 Jun to Sun 27 Sep 2026.</summary>
    private static readonly GoalRaceRequest FirstMarathon = new("Vienna Marathon", new DateOnly(2026, 9, 27), Marathon, null, 14);

    /// <summary>Sun 23 May 2027, 16 weeks: Mon 1 Feb to Sun 23 May 2027.</summary>
    private static readonly GoalRaceRequest SecondMarathon = new("Second marathon", new DateOnly(2027, 5, 23), Marathon, 5 * 3600, 16);

    private readonly TestApp _app = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Between_blocks_the_page_counts_down_to_the_next_one_with_recent_weekly_distance()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Long ago", new DateTime(2026, 8, 9, 9, 0, 0), 30000, 11000),
            new TestActivity(2, "Recovery jog", new DateTime(2026, 9, 30, 18, 0, 0), 6000, 2400),
            new TestActivity(3, "Easy run", new DateTime(2026, 10, 2, 18, 0, 0), 8000, 3100),
            new TestActivity(4, "Evening Run", new DateTime(2026, 10, 6, 20, 35, 0), 5081.2, 2096));
        await CreateGoalRaceAsync(browser, FirstMarathon);
        var second = await CreateGoalRaceAsync(browser, SecondMarathon);

        var training = await GetTrainingAsync(browser);

        Assert.Equal(("countdown", null), (training.View, training.Block));
        var countdown = training.Countdown!;
        Assert.Equal((second.Id, "Second marathon"), (countdown.GoalRace.Id, countdown.GoalRace.Name));
        Assert.Equal(new DateOnly(2027, 2, 1), countdown.TrainingBlockStart);
        Assert.Equal(115, countdown.DaysRemaining);
        // The last 5 Training Weeks up to the current one, oldest first; weeks without Runs as zero.
        Assert.Equal(
            [
                new RecentWeekResponse(new DateOnly(2026, 9, 7), 0),
                new RecentWeekResponse(new DateOnly(2026, 9, 14), 0),
                new RecentWeekResponse(new DateOnly(2026, 9, 21), 0),
                new RecentWeekResponse(new DateOnly(2026, 9, 28), 14000),
                new RecentWeekResponse(new DateOnly(2026, 10, 5), 5081.2),
            ],
            countdown.RecentWeeks);
    }

    [Fact]
    public async Task The_day_before_the_block_still_counts_down_one_day()
    {
        var browser = _app.CreateBrowser();
        await CreateGoalRaceAsync(browser, FirstMarathon);
        await CreateGoalRaceAsync(browser, SecondMarathon);
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 1, 31, 12, 0, 0, TimeSpan.Zero));

        var training = await GetTrainingAsync(browser);

        Assert.Equal("countdown", training.View);
        Assert.Equal((new DateOnly(2027, 2, 1), 1), (training.Countdown!.TrainingBlockStart, training.Countdown.DaysRemaining));
        Assert.Equal(new DateOnly(2027, 1, 25), training.Countdown.RecentWeeks[^1].Start);
    }

    [Fact]
    public async Task On_the_first_day_of_the_block_the_page_shows_the_block()
    {
        var browser = _app.CreateBrowser();
        await CreateGoalRaceAsync(browser, FirstMarathon);
        var second = await CreateGoalRaceAsync(browser, SecondMarathon);
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 2, 1, 12, 0, 0, TimeSpan.Zero));

        var training = await GetTrainingAsync(browser);

        Assert.Equal(("block", second.Id, null), (training.View, training.Block!.GoalRace.Id, training.Countdown));
    }

    [Fact]
    public async Task Without_an_upcoming_goal_race_there_is_nothing_to_count_down_to()
    {
        var browser = _app.CreateBrowser();
        await CreateGoalRaceAsync(browser, FirstMarathon);

        var training = await GetTrainingAsync(browser);

        Assert.Equal(("none", null, null), (training.View, training.Block, training.Countdown));
    }

    private static async Task<GoalRaceResponse> CreateGoalRaceAsync(HttpClient browser, GoalRaceRequest race)
    {
        var response = await browser.PostAsJsonAsync("/api/goal-races", race);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GoalRaceResponse>())!;
    }

    private static async Task<TrainingResponse> GetTrainingAsync(HttpClient browser) =>
        (await browser.GetFromJsonAsync<TrainingResponse>("/api/training"))!;

    /// <param name="TargetTime">Seconds.</param>
    private sealed record GoalRaceRequest(string Name, DateOnly Date, double Distance, int? TargetTime, int TrainingBlockWeeks);

    private sealed record GoalRaceResponse(int Id, string Name);

    /// <param name="View">"block", "countdown" or "none".</param>
    private sealed record TrainingResponse(string View, BlockResponse? Block, CountdownResponse? Countdown);

    private sealed record BlockResponse(GoalRaceResponse GoalRace);

    /// <param name="TrainingBlockStart">The Monday the next Training Block starts.</param>
    private sealed record CountdownResponse(GoalRaceResponse GoalRace, DateOnly TrainingBlockStart, int DaysRemaining, List<RecentWeekResponse> RecentWeeks);

    /// <param name="Start">The Monday.</param>
    /// <param name="Distance">Metres.</param>
    private sealed record RecentWeekResponse(DateOnly Start, double Distance);
}
