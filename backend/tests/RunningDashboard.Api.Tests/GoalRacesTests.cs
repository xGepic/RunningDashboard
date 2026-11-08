using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;
using static RunningDashboard.Api.Tests.Harness.TestRaces;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// The Athlete's first marathon (Sun 27 Sep 2026, 14-week Training Block, no Target Time) and second marathon
/// (Sun 23 May 2027, 16 weeks, Target Time 5:00:00).
/// </summary>
public sealed class GoalRacesTests
{
    /// <summary>Sun 10 Oct 2027, 12 weeks: Mon 19 Jul to Sun 10 Oct 2027.</summary>
    private static readonly GoalRaceRequest AutumnHalf = new("Autumn half", new DateOnly(2027, 10, 10), 21097, null, 12);

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
    public async Task The_first_marathon_gets_a_training_block_from_monday_22_june_to_race_day()
    {
        var created = await _browser.CreateGoalRaceAsync(FirstMarathon);
        var listed = (await _browser.ListGoalRacesAsync()).ShouldHaveSingleItem();

        listed.ShouldBe(created);
        listed.ShouldBe(
            new GoalRaceResponse(created.Id, "Vienna Marathon", new DateOnly(2026, 9, 27), Marathon, null, 14, new DateOnly(2026, 6, 22), new DateOnly(2026, 9, 27), null, null));
    }

    [Test]
    public async Task A_goal_race_whose_training_block_overlaps_another_is_rejected()
    {
        await _browser.CreateGoalRaceAsync(FirstMarathon);

        // Sun 11 Oct 2026, 4 weeks: from Mon 14 Sep, so it takes in the first marathon's last two weeks.
        var response = await _browser.PostAsJsonAsync("/api/goal-races", new GoalRaceRequest("Autumn 10K", new DateOnly(2026, 10, 11), 10000, null, 4));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem!.Errors["trainingBlockWeeks"].ShouldHaveSingleItem().ShouldBe(
            "The Training Block (14 Sep – 11 Oct 2026) overlaps the one of Vienna Marathon (22 Jun – 27 Sep 2026).");
        (await _browser.ListGoalRacesAsync()).ShouldHaveSingleItem();
    }

    [Test]
    public async Task A_training_block_may_start_the_monday_after_another_race_week()
    {
        await _browser.CreateGoalRaceAsync(FirstMarathon);

        // Sun 18 Oct 2026, 3 weeks: from Mon 28 Sep.
        await _browser.CreateGoalRaceAsync(new GoalRaceRequest("Autumn 10K", new DateOnly(2026, 10, 18), 10000, null, 3));

        // Listed newest first.
        (await _browser.ListGoalRacesAsync()).Select(r => r.Name).ShouldBe(["Autumn 10K", "Vienna Marathon"]);
    }

    [Test]
    public async Task Editing_the_date_moves_the_training_block()
    {
        var created = await _browser.CreateGoalRaceAsync(FirstMarathon);

        // One week later; the new block still covers most of the old one, which is no overlap with itself.
        var response = await _browser.PutAsJsonAsync($"/api/goal-races/{created.Id}", FirstMarathon with { Date = new DateOnly(2026, 10, 4) });
        var listed = (await _browser.ListGoalRacesAsync()).ShouldHaveSingleItem();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        listed.Date.ShouldBe(new DateOnly(2026, 10, 4));
        listed.TrainingBlockStart.ShouldBe(new DateOnly(2026, 6, 29));
        listed.TrainingBlockEnd.ShouldBe(new DateOnly(2026, 10, 4));
    }

    [Test]
    public async Task Editing_a_race_into_another_ones_training_block_is_rejected()
    {
        await _browser.CreateGoalRaceAsync(FirstMarathon);
        var second = await _browser.CreateGoalRaceAsync(SecondMarathon);

        var response = await _browser.PutAsJsonAsync($"/api/goal-races/{second.Id}", SecondMarathon with { TrainingBlockWeeks = 40 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _browser.ListGoalRacesAsync()).Single(r => r.Id == second.Id).TrainingBlockWeeks.ShouldBe(16);
    }

    [Test]
    public async Task A_second_upcoming_goal_race_is_rejected()
    {
        await _browser.CreateGoalRaceAsync(SecondMarathon);

        // Sun 10 Oct 2027, 12 weeks: from Mon 19 Jul, well clear of the second marathon's block.
        var response = await _browser.PostAsJsonAsync("/api/goal-races", AutumnHalf);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem!.Errors["date"].ShouldHaveSingleItem().ShouldBe(
            "Second marathon on 23 May 2027 is still to come, and only one Goal Race can be upcoming at a time.");
        (await _browser.ListGoalRacesAsync()).ShouldHaveSingleItem();
    }

    [Test]
    public async Task Past_goal_races_can_be_added_while_one_is_upcoming()
    {
        await _browser.CreateGoalRaceAsync(SecondMarathon);
        await _browser.CreateGoalRaceAsync(FirstMarathon);

        (await _browser.ListGoalRacesAsync()).Select(r => r.Name).ShouldBe(["Second marathon", "Vienna Marathon"]);
    }

    [Test]
    public async Task The_next_goal_race_can_be_added_once_race_day_of_the_upcoming_one_is_over()
    {
        await _browser.CreateGoalRaceAsync(SecondMarathon);

        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 5, 23, 18, 0, 0, TimeSpan.Zero));
        var onRaceDay = await _browser.PostAsJsonAsync("/api/goal-races", AutumnHalf);
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 5, 24, 8, 0, 0, TimeSpan.Zero));
        var dayAfter = await _browser.PostAsJsonAsync("/api/goal-races", AutumnHalf);

        onRaceDay.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        dayAfter.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Test]
    public async Task Moving_a_past_goal_race_into_the_future_is_rejected_while_another_is_upcoming()
    {
        var first = await _browser.CreateGoalRaceAsync(FirstMarathon);
        await _browser.CreateGoalRaceAsync(SecondMarathon);

        var response = await _browser.PutAsJsonAsync($"/api/goal-races/{first.Id}", FirstMarathon with { Date = new DateOnly(2027, 10, 10) });
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem!.Errors.Keys.ShouldBe(["date"]);
        (await _browser.ListGoalRacesAsync()).Single(r => r.Id == first.Id).Date.ShouldBe(new DateOnly(2026, 9, 27));
    }

    [Test]
    public async Task An_unknown_goal_race_is_not_found()
    {
        var response = await _browser.PutAsJsonAsync("/api/goal-races/999", FirstMarathon);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_deleted_goal_race_is_no_longer_listed_and_frees_its_training_block()
    {
        var first = await _browser.CreateGoalRaceAsync(FirstMarathon);

        var response = await _browser.DeleteAsync($"/api/goal-races/{first.Id}");
        var overlapping = await _browser.PostAsJsonAsync("/api/goal-races", new GoalRaceRequest("Autumn 10K", new DateOnly(2026, 10, 11), 10000, null, 4));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        overlapping.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await _browser.ListGoalRacesAsync()).Select(r => r.Name).ShouldBe(["Autumn 10K"]);
    }

    [Test]
    public async Task Each_missing_or_invalid_field_is_reported()
    {
        var response = await _browser.PostAsJsonAsync("/api/goal-races", new GoalRaceRequest(" ", null, 0, -1, 0));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem!.Errors.Keys.Order().ShouldBe(["date", "distance", "name", "targetTime", "trainingBlockWeeks"]);
        (await _browser.ListGoalRacesAsync()).ShouldBeEmpty();
    }

    [TestCase(1, 6, 1)]
    [TestCase(9999, 12, 31)]
    public async Task A_date_far_outside_any_running_career_is_reported(int year, int month, int day)
    {
        var response = await _browser.PostAsJsonAsync("/api/goal-races", FirstMarathon with { Date = new DateOnly(year, month, day) });
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem!.Errors.Keys.ShouldBe(["date"]);
    }
}
