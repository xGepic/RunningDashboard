using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// The Training page's block view: a Goal Race's Training Block week by week, with what was actually run. The clock
/// stands at Friday 9 Oct 2026 unless a test moves it.
/// </summary>
public sealed class TrainingBlockTests : IAsyncLifetime
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
    public async Task The_14_week_first_marathon_block_opens_with_what_was_run_each_week()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Sunday before the block", new DateTime(2026, 6, 21, 18, 0, 0), 10000, 3900, RelativeEffort: 50),
            new TestActivity(2, "Tuesday easy", new DateTime(2026, 6, 23, 18, 0, 0), 8000, 2880, RelativeEffort: 40),
            new TestActivity(3, "Sunday long run", new DateTime(2026, 6, 28, 8, 0, 0), 16000, 6240, RelativeEffort: 95),
            new TestActivity(4, "Finisher.", new DateTime(2026, 9, 27, 9, 47, 0), 42582.8, 18043, RelativeEffort: 310));
        var race = await CreateGoalRaceAsync(browser, FirstMarathon);

        var block = (await GetTrainingAsync(browser, $"?goalRaceId={race.Id}")).Block!;

        Assert.Equal(race.Id, block.GoalRace.Id);
        Assert.Null(block.TargetPace);
        Assert.Equal(Enumerable.Range(-13, 14), block.Weeks.Select(w => w.WeeksToRace));
        Assert.All(block.Weeks, w => Assert.Equal("past", w.Status));

        var first = block.Weeks[0];
        Assert.Equal((new DateOnly(2026, 6, 22), new DateOnly(2026, 6, 28)), (first.Start, first.End));
        Assert.Equal(
            new ActualResponse(2, 24000, 9120, 135, new LongRunResponse(3, "Sunday long run", 16000, 390, null)),
            first.Actual);

        Assert.Equal(new ActualResponse(0, 0, 0, null, null), block.Weeks[1].Actual);

        var raceWeek = block.Weeks[^1];
        Assert.Equal((new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27)), (raceWeek.Start, raceWeek.End));
        Assert.Equal(4, raceWeek.Actual!.LongRun!.RunId);
    }

    [Theory]
    [InlineData("2027-02-01", -15, "2027-02-01")]
    // Wednesday in the week from Mon 8 Mar, ten weeks before the Race Week from Mon 17 May.
    [InlineData("2027-03-10", -10, "2027-03-08")]
    [InlineData("2027-03-03", -11, "2027-03-01")]
    [InlineData("2027-05-23", 0, "2027-05-17")]
    public async Task Today_inside_a_training_block_opens_it_at_the_current_week(string today, int weeksToRace, string weekStart)
    {
        var browser = _app.CreateBrowser();
        await CreateGoalRaceAsync(browser, FirstMarathon);
        var second = await CreateGoalRaceAsync(browser, SecondMarathon);
        _app.Clock.SetUtcNow(new DateTimeOffset(DateOnly.Parse(today).ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

        var training = await GetTrainingAsync(browser);

        Assert.Equal("block", training.View);
        Assert.Equal(second.Id, training.Block!.GoalRace.Id);
        var current = Assert.Single(training.Block.Weeks, w => w.Status == "current");
        Assert.Equal(weeksToRace, current.WeeksToRace);
        Assert.Equal(DateOnly.Parse(weekStart), current.Start);
        Assert.All(training.Block.Weeks.Where(w => w.WeeksToRace > weeksToRace), w => Assert.Equal(("future", null), (w.Status, w.Actual)));
        Assert.All(training.Block.Weeks.Where(w => w.WeeksToRace < weeksToRace), w => Assert.Equal("past", w.Status));
    }

    [Fact]
    public async Task The_long_run_is_the_weeks_longest_run_compared_with_the_target_pace()
    {
        // Week −11 of the second marathon, Mon 1 to Sun 7 Mar 2027.
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Tuesday tempo", new DateTime(2027, 3, 2, 18, 0, 0), 10000, 3300, UtcOffsetHours: 1),
            new TestActivity(2, "Saturday long run", new DateTime(2027, 3, 6, 8, 0, 0), 18000, 7200, UtcOffsetHours: 1),
            new TestActivity(3, "Sunday long run", new DateTime(2027, 3, 7, 8, 0, 0), 18000, 7740, UtcOffsetHours: 1));
        var race = await CreateGoalRaceAsync(browser, SecondMarathon);
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero));

        var block = (await GetTrainingAsync(browser)).Block!;
        var week = block.Weeks.Single(w => w.WeeksToRace == -11);

        // 5:00:00 over 42.195 km is 426.6 s/km; to the second, 7:06 /km still gets there in time, 7:07 does not.
        Assert.Equal(426, block.TargetPace);
        // Equally long, so the one that took longer: 7,740 s over 18 km is 430 s/km (7:10), 4 s/km slower than 7:06.
        var longRun = week.Actual!.LongRun!;
        Assert.Equal((3, "Sunday long run", 18000, 430), (longRun.RunId, longRun.Name, longRun.Distance, longRun.Pace));
        Assert.Equal(4, longRun.PaceVsTarget);
        Assert.Equal(race.Id, block.GoalRace.Id);
    }

    [Fact]
    public async Task Outside_every_training_block_there_is_no_block_to_show()
    {
        var browser = _app.CreateBrowser();
        // Fri 9 Oct 2026 is after the first marathon's block and before the second one's.
        await CreateGoalRaceAsync(browser, FirstMarathon);
        await CreateGoalRaceAsync(browser, SecondMarathon);

        var training = await GetTrainingAsync(browser);

        Assert.Equal(("none", null), (training.View, training.Block));
    }

    [Fact]
    public async Task An_unknown_goal_race_is_not_found()
    {
        var response = await _app.CreateBrowser().GetAsync("/api/training?goalRaceId=999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<GoalRaceResponse> CreateGoalRaceAsync(HttpClient browser, GoalRaceRequest race)
    {
        var response = await browser.PostAsJsonAsync("/api/goal-races", race);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GoalRaceResponse>())!;
    }

    private static async Task<TrainingResponse> GetTrainingAsync(HttpClient browser, string query = "") =>
        (await browser.GetFromJsonAsync<TrainingResponse>($"/api/training{query}"))!;

    /// <param name="TargetTime">Seconds.</param>
    private sealed record GoalRaceRequest(string Name, DateOnly Date, double Distance, int? TargetTime, int TrainingBlockWeeks);

    private sealed record GoalRaceResponse(int Id, string Name);

    /// <param name="View">"block" while a Training Block is shown.</param>
    private sealed record TrainingResponse(string View, BlockResponse? Block);

    /// <param name="TargetPace">Whole seconds per km.</param>
    private sealed record BlockResponse(GoalRaceResponse GoalRace, int? TargetPace, List<WeekResponse> Weeks);

    /// <param name="Status">"past", "current" or "future".</param>
    private sealed record WeekResponse(int WeeksToRace, DateOnly Start, DateOnly End, string Status, ActualResponse? Actual);

    /// <param name="Distance">Metres.</param>
    /// <param name="MovingTime">Seconds.</param>
    private sealed record ActualResponse(int Runs, double Distance, int MovingTime, double? RelativeEffort, LongRunResponse? LongRun);

    /// <param name="Pace">Seconds per km.</param>
    /// <param name="PaceVsTarget">Whole seconds per km slower (positive) or faster (negative) than the Target Pace.</param>
    private sealed record LongRunResponse(long RunId, string Name, double Distance, double Pace, int? PaceVsTarget);
}
