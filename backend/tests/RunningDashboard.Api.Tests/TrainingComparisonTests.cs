using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// A Training Block compared week by week with another one, lined up by Weeks to Race. The Goal Races: a half
/// marathon (Sun 12 Apr 2026, 8 weeks), the first marathon (Sun 27 Sep 2026, 14 weeks) and the second marathon
/// (Sun 23 May 2027, 16 weeks).
/// </summary>
public sealed class TrainingComparisonTests : IAsyncLifetime
{
    private const double Marathon = 42195;

    private static readonly GoalRaceRequest HalfMarathon = new("Vienna Half", new DateOnly(2026, 4, 12), 21097.5, null, 8);

    private static readonly GoalRaceRequest FirstMarathon = new("Vienna Marathon", new DateOnly(2026, 9, 27), Marathon, null, 14);

    private static readonly GoalRaceRequest SecondMarathon = new("Second marathon", new DateOnly(2027, 5, 23), Marathon, 5 * 3600, 16);

    private readonly TestApp _app = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task A_16_week_block_lines_up_with_a_14_week_one_by_weeks_to_race()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            // Week −13 of the first marathon, and of the second.
            new TestActivity(1, "First block, first long run", new DateTime(2026, 6, 28, 8, 0, 0), 16000, 6240, RelativeEffort: 95),
            new TestActivity(2, "Second block, week −13", new DateTime(2027, 2, 21, 8, 0, 0), 18000, 7560, UtcOffsetHours: 1));
        var first = await CreateGoalRaceAsync(browser, FirstMarathon);
        var second = await CreateGoalRaceAsync(browser, SecondMarathon);
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero));

        var block = (await GetTrainingAsync(browser)).Block!;

        Assert.Equal(second.Id, block.GoalRace.Id);
        var comparison = block.Comparison!;
        Assert.Equal((first.Id, "Vienna Marathon"), (comparison.GoalRace.Id, comparison.GoalRace.Name));
        Assert.Equal(Enumerable.Range(-15, 16), comparison.Weeks.Select(w => w.WeeksToRace));
        Assert.All(comparison.Weeks, w => Assert.Equal(new DateOnly(2027, 5, 17).AddDays(7 * w.WeeksToRace), w.Viewed!.Start));
        // Weeks −15 and −14 have nothing to compare against.
        Assert.Equal([-15, -14], comparison.Weeks.Where(w => w.Compared is null).Select(w => w.WeeksToRace));

        var weekMinus13 = comparison.Weeks.Single(w => w.WeeksToRace == -13);
        Assert.Equal(2, weekMinus13.Viewed!.Actual!.LongRun!.RunId);
        Assert.Equal((new DateOnly(2026, 6, 22), "past"), (weekMinus13.Compared!.Start, weekMinus13.Compared.Status));
        // Last time's Long Run against the Pace needed now: 390 s/km is 36 s/km faster than 7:06.
        Assert.Equal(
            new ActualResponse(1, 16000, 6240, 95, new LongRunResponse(1, "First block, first long run", 16000, 390, -36)),
            weekMinus13.Compared.Actual);
    }

    [Fact]
    public async Task Compared_with_a_longer_block_the_viewed_block_has_nothing_in_its_earliest_weeks()
    {
        var browser = _app.CreateBrowser();
        var first = await CreateGoalRaceAsync(browser, FirstMarathon);
        var second = await CreateGoalRaceAsync(browser, SecondMarathon);

        var comparison = (await GetBlockAsync(browser, first, $"&compareWith={second.Id}")).Comparison!;

        Assert.Equal(Enumerable.Range(-15, 16), comparison.Weeks.Select(w => w.WeeksToRace));
        Assert.Equal([-15, -14], comparison.Weeks.Where(w => w.Viewed is null).Select(w => w.WeeksToRace));
        Assert.All(comparison.Weeks, w => Assert.NotNull(w.Compared));
    }

    [Fact]
    public async Task By_default_a_block_is_compared_with_the_most_recent_goal_race_before_it()
    {
        var browser = _app.CreateBrowser();
        // Entered out of order, to show the choice goes by race date.
        var second = await CreateGoalRaceAsync(browser, SecondMarathon);
        var half = await CreateGoalRaceAsync(browser, HalfMarathon);
        var first = await CreateGoalRaceAsync(browser, FirstMarathon);

        Assert.Equal(first.Id, (await GetBlockAsync(browser, second)).Comparison!.GoalRace.Id);
        Assert.Equal(half.Id, (await GetBlockAsync(browser, first)).Comparison!.GoalRace.Id);
        Assert.Null((await GetBlockAsync(browser, half)).Comparison);
    }

    [Fact]
    public async Task The_athlete_can_compare_with_another_goal_race_or_turn_the_comparison_off()
    {
        var browser = _app.CreateBrowser();
        var half = await CreateGoalRaceAsync(browser, HalfMarathon);
        await CreateGoalRaceAsync(browser, FirstMarathon);
        var second = await CreateGoalRaceAsync(browser, SecondMarathon);

        var withHalf = await GetBlockAsync(browser, second, $"&compareWith={half.Id}");
        var off = await GetBlockAsync(browser, second, "&compareWith=none");

        Assert.Equal(half.Id, withHalf.Comparison!.GoalRace.Id);
        Assert.Equal(Enumerable.Range(-7, 8), withHalf.Comparison.Weeks.Where(w => w.Compared is not null).Select(w => w.WeeksToRace));
        Assert.Null(off.Comparison);
    }

    [Theory]
    [InlineData("999", HttpStatusCode.NotFound)]
    [InlineData("previous", HttpStatusCode.BadRequest)]
    [InlineData("self", HttpStatusCode.BadRequest)]
    public async Task Comparing_with_an_unknown_race_or_the_race_itself_is_refused(string compareWith, HttpStatusCode expected)
    {
        var browser = _app.CreateBrowser();
        var second = await CreateGoalRaceAsync(browser, SecondMarathon);
        if (compareWith == "self")
            compareWith = second.Id.ToString();

        var response = await browser.GetAsync($"/api/training?goalRaceId={second.Id}&compareWith={compareWith}");

        Assert.Equal(expected, response.StatusCode);
    }

    private static async Task<BlockResponse> GetBlockAsync(HttpClient browser, GoalRaceResponse race, string query = "") =>
        (await GetTrainingAsync(browser, $"?goalRaceId={race.Id}{query}")).Block!;

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

    private sealed record TrainingResponse(string View, BlockResponse? Block);

    /// <param name="Comparison">The block it is compared with; none when the comparison is off.</param>
    private sealed record BlockResponse(GoalRaceResponse GoalRace, List<WeekResponse> Weeks, ComparisonResponse? Comparison);

    /// <param name="Weeks">One per Weeks to Race in either block, first to Race Week.</param>
    private sealed record ComparisonResponse(GoalRaceResponse GoalRace, List<LinedUpWeekResponse> Weeks);

    /// <param name="Viewed">The viewed block's week; none if that block does not have it.</param>
    /// <param name="Compared">The compared block's week; none if that block does not have it.</param>
    private sealed record LinedUpWeekResponse(int WeeksToRace, WeekResponse? Viewed, WeekResponse? Compared);

    /// <param name="Status">"past", "current" or "future".</param>
    private sealed record WeekResponse(int WeeksToRace, DateOnly Start, string Status, ActualResponse? Actual);

    /// <param name="Distance">Metres.</param>
    /// <param name="MovingTime">Seconds.</param>
    private sealed record ActualResponse(int Runs, double Distance, int MovingTime, double? RelativeEffort, LongRunResponse? LongRun);

    /// <param name="Pace">Seconds per km.</param>
    /// <param name="PaceVsTarget">Whole seconds per km against the viewed Goal Race's Target Pace.</param>
    private sealed record LongRunResponse(long RunId, string Name, double Distance, double Pace, int? PaceVsTarget);
}
