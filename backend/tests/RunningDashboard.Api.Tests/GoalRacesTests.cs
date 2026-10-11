using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// The Athlete's first marathon (Sun 27 Sep 2026, 14-week Training Block, no Target Time) and second marathon
/// (Sun 23 May 2027, 16 weeks, Target Time 5:00:00).
/// </summary>
public sealed class GoalRacesTests : IAsyncLifetime
{
    private const double Marathon = 42195;

    private static readonly GoalRaceRequest FirstMarathon = new("Vienna Marathon", new DateOnly(2026, 9, 27), Marathon, null, 14);

    private static readonly GoalRaceRequest SecondMarathon = new("Second marathon", new DateOnly(2027, 5, 23), Marathon, 5 * 3600, 16);

    /// <summary>Sun 10 Oct 2027, 12 weeks: Mon 19 Jul to Sun 10 Oct 2027.</summary>
    private static readonly GoalRaceRequest AutumnHalf = new("Autumn half", new DateOnly(2027, 10, 10), 21097, null, 12);

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    public Task InitializeAsync()
    {
        _browser = _app.CreateBrowser();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task The_first_marathon_gets_a_training_block_from_monday_22_june_to_race_day()
    {
        var created = await CreateAsync(FirstMarathon);
        var listed = Assert.Single(await ListAsync());

        Assert.Equal(created, listed);
        Assert.Equal(
            new GoalRaceResponse(created.Id, "Vienna Marathon", new DateOnly(2026, 9, 27), Marathon, null, 14, new DateOnly(2026, 6, 22), new DateOnly(2026, 9, 27)),
            listed);
    }

    [Fact]
    public async Task A_goal_race_whose_training_block_overlaps_another_is_rejected()
    {
        await CreateAsync(FirstMarathon);

        // Sun 11 Oct 2026, 4 weeks: from Mon 14 Sep, so it takes in the first marathon's last two weeks.
        var response = await _browser.PostAsJsonAsync("/api/goal-races", new GoalRaceRequest("Autumn 10K", new DateOnly(2026, 10, 11), 10000, null, 4));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "The Training Block (14 Sep – 11 Oct 2026) overlaps the one of Vienna Marathon (22 Jun – 27 Sep 2026).",
            Assert.Single(problem!.Errors["trainingBlockWeeks"]));
        Assert.Single(await ListAsync());
    }

    [Fact]
    public async Task A_training_block_may_start_the_monday_after_another_race_week()
    {
        await CreateAsync(FirstMarathon);

        // Sun 18 Oct 2026, 3 weeks: from Mon 28 Sep.
        await CreateAsync(new GoalRaceRequest("Autumn 10K", new DateOnly(2026, 10, 18), 10000, null, 3));

        // Listed newest first.
        Assert.Equal(["Autumn 10K", "Vienna Marathon"], (await ListAsync()).Select(r => r.Name));
    }

    [Fact]
    public async Task Editing_the_date_moves_the_training_block()
    {
        var created = await CreateAsync(FirstMarathon);

        // One week later; the new block still covers most of the old one, which is no overlap with itself.
        var response = await _browser.PutAsJsonAsync($"/api/goal-races/{created.Id}", FirstMarathon with { Date = new DateOnly(2026, 10, 4) });
        var listed = Assert.Single(await ListAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new DateOnly(2026, 10, 4), listed.Date);
        Assert.Equal(new DateOnly(2026, 6, 29), listed.TrainingBlockStart);
        Assert.Equal(new DateOnly(2026, 10, 4), listed.TrainingBlockEnd);
    }

    [Fact]
    public async Task Editing_a_race_into_another_ones_training_block_is_rejected()
    {
        await CreateAsync(FirstMarathon);
        var second = await CreateAsync(SecondMarathon);

        var response = await _browser.PutAsJsonAsync($"/api/goal-races/{second.Id}", SecondMarathon with { TrainingBlockWeeks = 40 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(16, (await ListAsync()).Single(r => r.Id == second.Id).TrainingBlockWeeks);
    }

    [Fact]
    public async Task A_second_upcoming_goal_race_is_rejected()
    {
        await CreateAsync(SecondMarathon);

        // Sun 10 Oct 2027, 12 weeks: from Mon 19 Jul, well clear of the second marathon's block.
        var response = await _browser.PostAsJsonAsync("/api/goal-races", AutumnHalf);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "Second marathon on 23 May 2027 is still to come, and only one Goal Race can be upcoming at a time.",
            Assert.Single(problem!.Errors["date"]));
        Assert.Single(await ListAsync());
    }

    [Fact]
    public async Task Past_goal_races_can_be_added_while_one_is_upcoming()
    {
        await CreateAsync(SecondMarathon);
        await CreateAsync(FirstMarathon);

        Assert.Equal(["Second marathon", "Vienna Marathon"], (await ListAsync()).Select(r => r.Name));
    }

    [Fact]
    public async Task The_next_goal_race_can_be_added_once_race_day_of_the_upcoming_one_is_over()
    {
        await CreateAsync(SecondMarathon);

        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 5, 23, 18, 0, 0, TimeSpan.Zero));
        var onRaceDay = await _browser.PostAsJsonAsync("/api/goal-races", AutumnHalf);
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 5, 24, 8, 0, 0, TimeSpan.Zero));
        var dayAfter = await _browser.PostAsJsonAsync("/api/goal-races", AutumnHalf);

        Assert.Equal(HttpStatusCode.BadRequest, onRaceDay.StatusCode);
        Assert.Equal(HttpStatusCode.Created, dayAfter.StatusCode);
    }

    [Fact]
    public async Task Moving_a_past_goal_race_into_the_future_is_rejected_while_another_is_upcoming()
    {
        var first = await CreateAsync(FirstMarathon);
        await CreateAsync(SecondMarathon);

        var response = await _browser.PutAsJsonAsync($"/api/goal-races/{first.Id}", FirstMarathon with { Date = new DateOnly(2027, 10, 10) });
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["date"], problem!.Errors.Keys);
        Assert.Equal(new DateOnly(2026, 9, 27), (await ListAsync()).Single(r => r.Id == first.Id).Date);
    }

    [Fact]
    public async Task An_unknown_goal_race_is_not_found()
    {
        var response = await _browser.PutAsJsonAsync("/api/goal-races/999", FirstMarathon);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_deleted_goal_race_is_no_longer_listed_and_frees_its_training_block()
    {
        var first = await CreateAsync(FirstMarathon);

        var response = await _browser.DeleteAsync($"/api/goal-races/{first.Id}");
        var overlapping = await _browser.PostAsJsonAsync("/api/goal-races", new GoalRaceRequest("Autumn 10K", new DateOnly(2026, 10, 11), 10000, null, 4));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Created, overlapping.StatusCode);
        Assert.Equal(["Autumn 10K"], (await ListAsync()).Select(r => r.Name));
    }

    [Fact]
    public async Task Each_missing_or_invalid_field_is_reported()
    {
        var response = await _browser.PostAsJsonAsync("/api/goal-races", new GoalRaceRequest(" ", null, 0, -1, 0));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["date", "distance", "name", "targetTime", "trainingBlockWeeks"], problem!.Errors.Keys.Order());
        Assert.Empty(await ListAsync());
    }

    [Theory]
    [InlineData(1, 6, 1)]
    [InlineData(9999, 12, 31)]
    public async Task A_date_far_outside_any_running_career_is_reported(int year, int month, int day)
    {
        var response = await _browser.PostAsJsonAsync("/api/goal-races", FirstMarathon with { Date = new DateOnly(year, month, day) });
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["date"], problem!.Errors.Keys);
    }

    private async Task<GoalRaceResponse> CreateAsync(GoalRaceRequest race)
    {
        var response = await _browser.PostAsJsonAsync("/api/goal-races", race);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GoalRaceResponse>())!;
    }

    private async Task<List<GoalRaceResponse>> ListAsync() =>
        (await _browser.GetFromJsonAsync<List<GoalRaceResponse>>("/api/goal-races"))!;

    private sealed record ValidationProblem(string Title, Dictionary<string, string[]> Errors);

    /// <param name="TargetTime">Seconds.</param>
    private sealed record GoalRaceRequest(string? Name, DateOnly? Date, double? Distance, int? TargetTime, int? TrainingBlockWeeks);

    private sealed record GoalRaceResponse(
        int Id,
        string Name,
        DateOnly Date,
        double Distance,
        int? TargetTime,
        int TrainingBlockWeeks,
        DateOnly TrainingBlockStart,
        DateOnly TrainingBlockEnd);
}
