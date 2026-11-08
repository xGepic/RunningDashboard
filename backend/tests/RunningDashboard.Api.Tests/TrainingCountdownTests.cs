using RunningDashboard.Api.Tests.Harness;
using static RunningDashboard.Api.Tests.Harness.TestRaces;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// The Training page between Training Blocks: a countdown to the next one with recent weekly distance, or an empty
/// state without an upcoming Goal Race. The clock stands at Friday 9 Oct 2026 unless a test moves it.
/// </summary>
public sealed class TrainingCountdownTests
{
    private readonly TestApp _app = new();


    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Between_blocks_the_page_counts_down_to_the_next_one_with_recent_weekly_distance()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Long ago", new DateTime(2026, 8, 9, 9, 0, 0), 30000, 11000),
            new TestActivity(2, "Recovery jog", new DateTime(2026, 9, 30, 18, 0, 0), 6000, 2400),
            new TestActivity(3, "Easy run", new DateTime(2026, 10, 2, 18, 0, 0), 8000, 3100),
            new TestActivity(4, "Evening Run", new DateTime(2026, 10, 6, 20, 35, 0), 5081.2, 2096));
        await browser.CreateGoalRaceAsync(FirstMarathon);
        var second = await browser.CreateGoalRaceAsync(SecondMarathon);

        var training = await browser.GetTrainingAsync();

        (training.View, training.Block).ShouldBe(("countdown", null));
        var countdown = training.Countdown!;
        (countdown.GoalRace.Id, countdown.GoalRace.Name).ShouldBe((second.Id, "Second marathon"));
        countdown.TrainingBlockStart.ShouldBe(new DateOnly(2027, 2, 1));
        countdown.DaysRemaining.ShouldBe(115);
        // The last 5 Training Weeks up to the current one, oldest first; weeks without Runs as zero.
        countdown.RecentWeeks.ShouldBe(
            [
                new RecentWeekResponse(new DateOnly(2026, 9, 7), 0),
                new RecentWeekResponse(new DateOnly(2026, 9, 14), 0),
                new RecentWeekResponse(new DateOnly(2026, 9, 21), 0),
                new RecentWeekResponse(new DateOnly(2026, 9, 28), 14000),
                new RecentWeekResponse(new DateOnly(2026, 10, 5), 5081.2),
            ]);
    }

    [Test]
    public async Task The_day_before_the_block_still_counts_down_one_day()
    {
        var browser = _app.CreateBrowser();
        await browser.CreateGoalRaceAsync(FirstMarathon);
        await browser.CreateGoalRaceAsync(SecondMarathon);
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 1, 31, 12, 0, 0, TimeSpan.Zero));

        var training = await browser.GetTrainingAsync();

        training.View.ShouldBe("countdown");
        (training.Countdown!.TrainingBlockStart, training.Countdown.DaysRemaining).ShouldBe((new DateOnly(2027, 2, 1), 1));
        training.Countdown.RecentWeeks[^1].Start.ShouldBe(new DateOnly(2027, 1, 25));
    }

    [Test]
    public async Task On_the_first_day_of_the_block_the_page_shows_the_block()
    {
        var browser = _app.CreateBrowser();
        await browser.CreateGoalRaceAsync(FirstMarathon);
        var second = await browser.CreateGoalRaceAsync(SecondMarathon);
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 2, 1, 12, 0, 0, TimeSpan.Zero));

        var training = await browser.GetTrainingAsync();

        (training.View, training.Block!.GoalRace.Id, training.Countdown).ShouldBe(("block", second.Id, null));
    }

    [Test]
    public async Task Without_an_upcoming_goal_race_there_is_nothing_to_count_down_to()
    {
        var browser = _app.CreateBrowser();
        await browser.CreateGoalRaceAsync(FirstMarathon);

        var training = await browser.GetTrainingAsync();

        (training.View, training.Block, training.Countdown).ShouldBe(("none", null, null));
    }
}
