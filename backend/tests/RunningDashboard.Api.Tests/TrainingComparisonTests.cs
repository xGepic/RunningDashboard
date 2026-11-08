using System.Net;
using RunningDashboard.Api.Tests.Harness;
using static RunningDashboard.Api.Tests.Harness.TestRaces;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// A Training Block compared week by week with another one, lined up by Weeks to Race. The Goal Races: a half
/// marathon (Sun 12 Apr 2026, 8 weeks), the first marathon (Sun 27 Sep 2026, 14 weeks) and the second marathon
/// (Sun 23 May 2027, 16 weeks).
/// </summary>
public sealed class TrainingComparisonTests
{
    private static readonly GoalRaceRequest HalfMarathon = new("Vienna Half", new DateOnly(2026, 4, 12), 21097.5, null, 8);

    private readonly TestApp _app = new();


    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task A_16_week_block_lines_up_with_a_14_week_one_by_weeks_to_race()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            // Week −13 of the first marathon, and of the second.
            new TestActivity(1, "First block, first long run", new DateTime(2026, 6, 28, 8, 0, 0), 16000, 6240, RelativeEffort: 95),
            new TestActivity(2, "Second block, week −13", new DateTime(2027, 2, 21, 8, 0, 0), 18000, 7560, UtcOffsetHours: 1));
        var first = await browser.CreateGoalRaceAsync(FirstMarathon);
        var second = await browser.CreateGoalRaceAsync(SecondMarathon);
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero));

        var block = (await browser.GetTrainingAsync()).Block!;

        block.GoalRace.Id.ShouldBe(second.Id);
        var comparison = block.Comparison!;
        (comparison.GoalRace.Id, comparison.GoalRace.Name).ShouldBe((first.Id, "Vienna Marathon"));
        comparison.Weeks.Select(w => w.WeeksToRace).ShouldBe(Enumerable.Range(-15, 16));
        comparison.Weeks.ShouldAllBe(w => w.Viewed!.Start == new DateOnly(2027, 5, 17).AddDays(7 * w.WeeksToRace));
        // Weeks −15 and −14 have nothing to compare against.
        comparison.Weeks.Where(w => w.Compared is null).Select(w => w.WeeksToRace).ShouldBe([-15, -14]);

        var weekMinus13 = comparison.Weeks.Single(w => w.WeeksToRace == -13);
        weekMinus13.Viewed!.Actual!.LongRun!.RunId.ShouldBe(2);
        (weekMinus13.Compared!.Start, weekMinus13.Compared.Status).ShouldBe((new DateOnly(2026, 6, 22), "past"));
        // Last time's Long Run against the Pace needed now: 390 s/km is 36 s/km faster than 7:06.
        weekMinus13.Compared.Actual.ShouldBe(
            new ActualResponse(1, 16000, 6240, 95, new LongRunResponse(1, "First block, first long run", new DateTime(2026, 6, 28, 8, 0, 0), 16000, 390, -36)));
    }

    [Test]
    public async Task Compared_with_a_longer_block_the_viewed_block_has_nothing_in_its_earliest_weeks()
    {
        var browser = _app.CreateBrowser();
        var first = await browser.CreateGoalRaceAsync(FirstMarathon);
        var second = await browser.CreateGoalRaceAsync(SecondMarathon);

        var comparison = (await GetBlockAsync(browser, first, $"&compareWith={second.Id}")).Comparison!;

        comparison.Weeks.Select(w => w.WeeksToRace).ShouldBe(Enumerable.Range(-15, 16));
        comparison.Weeks.Where(w => w.Viewed is null).Select(w => w.WeeksToRace).ShouldBe([-15, -14]);
        comparison.Weeks.ShouldAllBe(w => w.Compared != null);
    }

    [Test]
    public async Task By_default_a_block_is_compared_with_the_most_recent_goal_race_before_it()
    {
        var browser = _app.CreateBrowser();
        // Entered out of order, to show the choice goes by race date.
        var second = await browser.CreateGoalRaceAsync(SecondMarathon);
        var half = await browser.CreateGoalRaceAsync(HalfMarathon);
        var first = await browser.CreateGoalRaceAsync(FirstMarathon);

        (await GetBlockAsync(browser, second)).Comparison!.GoalRace.Id.ShouldBe(first.Id);
        (await GetBlockAsync(browser, first)).Comparison!.GoalRace.Id.ShouldBe(half.Id);
        (await GetBlockAsync(browser, half)).Comparison.ShouldBeNull();
    }

    [Test]
    public async Task The_athlete_can_compare_with_another_goal_race_or_turn_the_comparison_off()
    {
        var browser = _app.CreateBrowser();
        var half = await browser.CreateGoalRaceAsync(HalfMarathon);
        await browser.CreateGoalRaceAsync(FirstMarathon);
        var second = await browser.CreateGoalRaceAsync(SecondMarathon);

        var withHalf = await GetBlockAsync(browser, second, $"&compareWith={half.Id}");
        var off = await GetBlockAsync(browser, second, "&compareWith=none");

        withHalf.Comparison!.GoalRace.Id.ShouldBe(half.Id);
        withHalf.Comparison.Weeks.Where(w => w.Compared is not null).Select(w => w.WeeksToRace).ShouldBe(Enumerable.Range(-7, 8));
        off.Comparison.ShouldBeNull();
    }

    [TestCase("999", HttpStatusCode.NotFound)]
    [TestCase("previous", HttpStatusCode.BadRequest)]
    [TestCase("self", HttpStatusCode.BadRequest)]
    public async Task Comparing_with_an_unknown_race_or_the_race_itself_is_refused(string compareWith, HttpStatusCode expected)
    {
        var browser = _app.CreateBrowser();
        var second = await browser.CreateGoalRaceAsync(SecondMarathon);
        if (compareWith == "self")
            compareWith = second.Id.ToString();

        var response = await browser.GetAsync($"/api/training?goalRaceId={second.Id}&compareWith={compareWith}");

        response.StatusCode.ShouldBe(expected);
    }

    private static async Task<BlockResponse> GetBlockAsync(HttpClient browser, GoalRaceResponse race, string query = "") =>
        (await browser.GetTrainingAsync($"goalRaceId={race.Id}{query}")).Block!;
}
