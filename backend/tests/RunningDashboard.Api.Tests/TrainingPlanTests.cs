using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// A Goal Race's Training Plan: its length (12, 16 or 20 weeks) and the type of each 4-week Phase. The second
/// marathon is on Sun 23 May 2027, so its Race Week starts Mon 17 May; on its own it has a 10-week block.
/// </summary>
public sealed class TrainingPlanTests : IAsyncLifetime
{
    private const double Marathon = 42195;

    private static readonly GoalRaceRequest FirstMarathon = new("Vienna Marathon", new DateOnly(2026, 9, 27), Marathon, null, 14);

    private static readonly GoalRaceRequest SecondMarathon = new("Second marathon", new DateOnly(2027, 5, 23), Marathon, 5 * 3600, 10);

    private static readonly TrainingPlanRequest BuildPeakPeakTaper = new(16, ["build", "peak", "peak", "taper"]);

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    public Task InitializeAsync()
    {
        _browser = _app.CreateBrowser();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task A_16_week_build_peak_peak_taper_plan_splits_into_phases_and_sets_the_training_block()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);

        var saved = await PutPlanAsync(race, BuildPeakPeakTaper);
        var plan = await GetPlanAsync(race);
        var listed = Assert.Single(await ListGoalRacesAsync());

        Assert.Equal(saved, plan);
        Assert.Equal(16, plan.Length);
        Assert.Equal(
            [new PhaseResponse("build", -15, -12), new PhaseResponse("peak", -11, -8), new PhaseResponse("peak", -7, -4), new PhaseResponse("taper", -3, 0)],
            plan.Phases);
        Assert.Equal(Enumerable.Range(-15, 16), plan.Weeks.Select(w => w.WeeksToRace));
        Assert.Equal(new WeekResponse(-15, new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 7), "build"), plan.Weeks[0]);
        Assert.Equal(new WeekResponse(0, new DateOnly(2027, 5, 17), new DateOnly(2027, 5, 23), "taper"), plan.Weeks[^1]);
        // The plan's 16 weeks are the Training Block now; the race's own 10 weeks only count without a plan.
        Assert.Equal((new DateOnly(2027, 2, 1), 16, 10), (listed.TrainingBlockStart, listed.TrainingPlanLength, listed.TrainingBlockWeeks));
    }

    [Theory]
    [InlineData(14, new[] { "build", "peak", "taper" }, "length")]
    [InlineData(null, new[] { "build", "peak", "peak", "taper" }, "length")]
    [InlineData(16, new[] { "build", "peak", "taper", "peak" }, "phases")]
    [InlineData(16, new[] { "build", "peak", "taper" }, "phases")]
    [InlineData(16, new[] { "build", "peak", "recovery", "taper" }, "phases")]
    [InlineData(16, new[] { "build", "7", "peak", "taper" }, "phases")]
    public async Task A_plan_must_be_12_16_or_20_weeks_of_4_week_phases_ending_with_a_taper(int? length, string[] phases, string field)
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);

        var response = await _browser.PutAsJsonAsync(PlanPath(race), new TrainingPlanRequest(length, phases));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([field], problem!.Errors.Keys);
        Assert.Equal(HttpStatusCode.NotFound, (await _browser.GetAsync(PlanPath(race))).StatusCode);
    }

    [Fact]
    public async Task The_last_phase_must_be_a_taper()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);

        var response = await _browser.PutAsJsonAsync(PlanPath(race), new TrainingPlanRequest(12, ["build", "peak", "peak"]));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        Assert.Equal("The last Phase must be a Taper.", Assert.Single(problem!.Errors["phases"]));
    }

    [Fact]
    public async Task Moving_the_race_date_moves_the_plan_with_it()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);

        var moved = await _browser.PutAsJsonAsync($"/api/goal-races/{race.Id}", SecondMarathon with { Date = new DateOnly(2027, 5, 30) });
        var plan = await GetPlanAsync(race);

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(new WeekResponse(-15, new DateOnly(2027, 2, 8), new DateOnly(2027, 2, 14), "build"), plan.Weeks[0]);
        Assert.Equal(new WeekResponse(0, new DateOnly(2027, 5, 24), new DateOnly(2027, 5, 30), "taper"), plan.Weeks[^1]);
        Assert.Equal(new DateOnly(2027, 2, 8), Assert.Single(await ListGoalRacesAsync()).TrainingBlockStart);
    }

    [Fact]
    public async Task Changing_the_length_keeps_the_weeks_still_in_range_and_adds_empty_ones()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);
        await PutWeeksAsync(race, new WeekRequest(-15, 30000, 12000, ["sunday"], false), new WeekRequest(-11, 35000, 14000, ["tuesday", "sunday"], true));

        var shorter = await PutPlanAsync(race, new TrainingPlanRequest(12, ["build", "peak", "taper"]));
        var longer = await PutPlanAsync(race, new TrainingPlanRequest(20, ["build", "build", "peak", "peak", "taper"]));
        var weeks = await GetPlannedWeeksAsync(race);

        Assert.Equal(Enumerable.Range(-11, 12), shorter.Weeks.Select(w => w.WeeksToRace));
        Assert.Equal(["build", "build", "build", "build"], shorter.Weeks.Take(4).Select(w => w.Phase));
        Assert.Equal(Enumerable.Range(-19, 20), longer.Weeks.Select(w => w.WeeksToRace));
        Assert.Equal(new DateOnly(2027, 1, 4), longer.Weeks[0].Start);
        Assert.Equal(new DateOnly(2027, 1, 4), Assert.Single(await ListGoalRacesAsync()).TrainingBlockStart);
        // Week −11 stayed in range throughout; week −15 was dropped by the 12-week plan and came back empty.
        Assert.Equal((-11, 35000.0, 14000.0, "tuesday sunday", true), weeks.Row(-11));
        Assert.Equal((-15, null, null, "", false), weeks.Row(-15));
    }

    [Fact]
    public async Task Weekly_rows_are_saved_and_read_back()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);

        var response = await _browser.PutAsJsonAsync(WeeksPath(race), new TrainingPlanWeeksRequest(
        [
            new WeekRequest(-15, 30000, 12000, ["tuesday", "thursday", "saturday", "sunday"], false),
            new WeekRequest(-12, 25000, 10000, ["sunday", "tuesday"], true),
            new WeekRequest(0, 55000, 42195, ["tuesday", "thursday", "sunday"], false),
        ]));
        var saved = await response.Content.ReadFromJsonAsync<PlannedWeeksResponse>();
        var read = await GetPlannedWeeksAsync(race);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(saved!.Rows(), read.Rows());
        Assert.Equal(16, read.Weeks.Count);
        Assert.Equal((-15, 30000.0, 12000.0, "tuesday thursday saturday sunday", false), read.Row(-15));
        // Running Days come back Monday first.
        Assert.Equal((-12, 25000.0, 10000.0, "tuesday sunday", true), read.Row(-12));
        Assert.Equal((0, 55000.0, 42195.0, "tuesday thursday sunday", false), read.Row(0));
        // Weeks left out keep what they had: nothing yet.
        Assert.Equal((-14, null, null, "", false), read.Row(-14));
    }

    [Fact]
    public async Task Saving_weeks_again_replaces_what_was_planned_for_them()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);
        await PutWeeksAsync(race, new WeekRequest(-15, 30000, 12000, ["tuesday", "sunday"], true));

        await PutWeeksAsync(race, new WeekRequest(-15, null, null, [], false));

        Assert.Equal((-15, null, null, "", false), (await GetPlannedWeeksAsync(race)).Row(-15));
    }

    [Fact]
    public async Task The_race_week_cannot_be_a_recovery_week()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);

        var response = await _browser.PutAsJsonAsync(WeeksPath(race), new TrainingPlanWeeksRequest(
        [
            new WeekRequest(-1, 40000, 16000, ["tuesday", "sunday"], false),
            new WeekRequest(0, 55000, 42195, ["sunday"], true),
        ]));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("The Race Week can't be a Recovery Week.", Assert.Single(problem!.Errors["weeks[1].recoveryWeek"]));
        // Nothing is saved, not even the valid week.
        Assert.Equal((-1, null, null, "", false), (await GetPlannedWeeksAsync(race)).Row(-1));
    }

    [Theory]
    [InlineData(-16, 30000.0, 12000.0, "tuesday", "weeks[0].weeksToRace")]
    [InlineData(1, 30000.0, 12000.0, "tuesday", "weeks[0].weeksToRace")]
    [InlineData(-15, 0.0, 12000.0, "tuesday", "weeks[0].plannedDistance")]
    [InlineData(-15, 30000.0, -1.0, "tuesday", "weeks[0].plannedLongRun")]
    [InlineData(-15, 30000.0, 12000.0, "funday", "weeks[0].runningDays")]
    [InlineData(-15, 30000.0, 12000.0, "3", "weeks[0].runningDays")]
    public async Task A_week_must_be_in_the_plan_with_positive_distances_and_real_days(int weeksToRace, double distance, double longRun, string day, string field)
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);

        var response = await _browser.PutAsJsonAsync(WeeksPath(race), new TrainingPlanWeeksRequest([new WeekRequest(weeksToRace, distance, longRun, [day], false)]));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([field], problem!.Errors.Keys);
    }

    [Fact]
    public async Task A_week_can_be_in_a_request_only_once()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);

        var response = await _browser.PutAsJsonAsync(WeeksPath(race), new TrainingPlanWeeksRequest(
            [new WeekRequest(-3, 30000, 12000, [], false), new WeekRequest(-3, 20000, 8000, [], false)]));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["weeks[1].weeksToRace"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task Weeks_of_a_goal_race_without_a_plan_cannot_be_saved()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);

        var response = await _browser.PutAsJsonAsync(WeeksPath(race), new TrainingPlanWeeksRequest([new WeekRequest(0, 55000, 42195, [], false)]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_plan_whose_block_would_overlap_another_goal_races_is_rejected()
    {
        await CreateGoalRaceAsync(FirstMarathon);
        // Sun 3 Jan 2027: 4 weeks from Mon 7 Dec 2026 are fine, 20 weeks from Mon 17 Aug 2026 overlap the first marathon.
        var race = await CreateGoalRaceAsync(new GoalRaceRequest("New Year half", new DateOnly(2027, 1, 3), 21097.5, null, 4));

        var response = await _browser.PutAsJsonAsync(PlanPath(race), new TrainingPlanRequest(20, ["build", "build", "peak", "peak", "taper"]));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "The Training Block (17 Aug 2026 – 3 Jan 2027) overlaps the one of Vienna Marathon (22 Jun – 27 Sep 2026).",
            Assert.Single(problem!.Errors["length"]));
    }

    [Fact]
    public async Task Another_goal_race_inside_a_planned_block_is_rejected_even_outside_the_races_own_block_length()
    {
        var second = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(second, BuildPeakPeakTaper);

        // Sun 14 Feb 2027, 2 weeks: clear of the race's own 10 weeks (from Mon 15 Mar), inside the plan's 16 (from Mon 1 Feb).
        var tuneUp = await _browser.PostAsJsonAsync("/api/goal-races", new GoalRaceRequest("Tune-up 10K", new DateOnly(2027, 2, 14), 10000, null, 2));
        // Lengthening the planned race's own block changes nothing while it has a plan.
        var edited = await _browser.PutAsJsonAsync($"/api/goal-races/{second.Id}", SecondMarathon with { TrainingBlockWeeks = 30 });

        Assert.Equal(HttpStatusCode.BadRequest, tuneUp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
    }

    [Fact]
    public async Task Deleting_the_plan_gives_the_race_back_its_own_block_length()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);
        await PutWeeksAsync(race, new WeekRequest(0, 55000, 42195, ["sunday"], false));

        var deleted = await _browser.DeleteAsync(PlanPath(race));
        var again = await _browser.DeleteAsync(PlanPath(race));
        var listed = Assert.Single(await ListGoalRacesAsync());

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _browser.GetAsync(PlanPath(race))).StatusCode);
        // Its own 10 weeks: from Mon 15 Mar 2027 instead of the plan's Mon 1 Feb.
        Assert.Equal((new DateOnly(2027, 3, 15), null), (listed.TrainingBlockStart, listed.TrainingPlanLength));
        // A new plan starts from nothing.
        await PutPlanAsync(race, BuildPeakPeakTaper);
        Assert.Equal((0, null, null, "", false), (await GetPlannedWeeksAsync(race)).Row(0));
    }

    [Fact]
    public async Task Deleting_a_goal_race_deletes_its_plan()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);

        var deleted = await _browser.DeleteAsync($"/api/goal-races/{race.Id}");
        var again = await CreateGoalRaceAsync(SecondMarathon);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _browser.GetAsync(PlanPath(again))).StatusCode);
        Assert.Equal(new DateOnly(2027, 3, 15), Assert.Single(await ListGoalRacesAsync()).TrainingBlockStart);
    }

    [Fact]
    public async Task A_goal_race_without_a_plan_or_an_unknown_one_has_none_to_show()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);

        Assert.Equal(HttpStatusCode.NotFound, (await _browser.GetAsync(PlanPath(race))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _browser.PutAsJsonAsync("/api/goal-races/999/training-plan", BuildPeakPeakTaper)).StatusCode);
    }

    private async Task<GoalRaceResponse> CreateGoalRaceAsync(GoalRaceRequest race)
    {
        var response = await _browser.PostAsJsonAsync("/api/goal-races", race);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GoalRaceResponse>())!;
    }

    private async Task<List<GoalRaceResponse>> ListGoalRacesAsync() =>
        (await _browser.GetFromJsonAsync<List<GoalRaceResponse>>("/api/goal-races"))!;

    private static string PlanPath(GoalRaceResponse race) => $"/api/goal-races/{race.Id}/training-plan";

    private async Task<TrainingPlanResponse> PutPlanAsync(GoalRaceResponse race, TrainingPlanRequest plan)
    {
        var response = await _browser.PutAsJsonAsync(PlanPath(race), plan);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TrainingPlanResponse>())!;
    }

    private async Task<TrainingPlanResponse> GetPlanAsync(GoalRaceResponse race) =>
        (await _browser.GetFromJsonAsync<TrainingPlanResponse>(PlanPath(race)))!;

    private static string WeeksPath(GoalRaceResponse race) => $"{PlanPath(race)}/weeks";

    private async Task PutWeeksAsync(GoalRaceResponse race, params WeekRequest[] weeks)
    {
        var response = await _browser.PutAsJsonAsync(WeeksPath(race), new TrainingPlanWeeksRequest([.. weeks]));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>The plan as GET returns it, read for what is planned each week.</summary>
    private async Task<PlannedWeeksResponse> GetPlannedWeeksAsync(GoalRaceResponse race) =>
        (await _browser.GetFromJsonAsync<PlannedWeeksResponse>(PlanPath(race)))!;

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);

    /// <param name="TargetTime">Seconds.</param>
    private sealed record GoalRaceRequest(string Name, DateOnly Date, double Distance, int? TargetTime, int TrainingBlockWeeks);

    /// <param name="TrainingPlanLength">None without a Training Plan.</param>
    private sealed record GoalRaceResponse(int Id, string Name, int TrainingBlockWeeks, DateOnly TrainingBlockStart, int? TrainingPlanLength);

    /// <param name="Length">12, 16 or 20 weeks.</param>
    /// <param name="Phases">The type of each 4-week Phase, first to last: "build", "peak" or "taper".</param>
    private sealed record TrainingPlanRequest(int? Length, string[]? Phases);

    private sealed record TrainingPlanResponse(int GoalRaceId, int Length, List<PhaseResponse> Phases, List<WeekResponse> Weeks)
    {
        // Records compare lists by reference; compare their contents.
        public bool Equals(TrainingPlanResponse? other) =>
            other is not null && GoalRaceId == other.GoalRaceId && Length == other.Length
            && Phases.SequenceEqual(other.Phases) && Weeks.SequenceEqual(other.Weeks);

        public override int GetHashCode() => HashCode.Combine(GoalRaceId, Length);
    }

    /// <param name="Type">"build", "peak" or "taper".</param>
    private sealed record PhaseResponse(string Type, int FirstWeeksToRace, int LastWeeksToRace);

    /// <param name="Phase">The type of the Phase the week is in.</param>
    private sealed record WeekResponse(int WeeksToRace, DateOnly Start, DateOnly End, string Phase);

    private sealed record TrainingPlanWeeksRequest(List<WeekRequest> Weeks);

    /// <param name="PlannedDistance">Metres, or none.</param>
    /// <param name="PlannedLongRun">Metres, or none.</param>
    /// <param name="RunningDays">"monday" to "sunday".</param>
    private sealed record WeekRequest(int WeeksToRace, double? PlannedDistance, double? PlannedLongRun, string[] RunningDays, bool RecoveryWeek);

    private sealed record PlannedWeeksResponse(List<PlannedWeekResponse> Weeks)
    {
        /// <summary>What is planned for one week, comparable as a whole: Running Days joined by spaces.</summary>
        public (int, double?, double?, string, bool) Row(int weeksToRace)
        {
            var week = Weeks.Single(w => w.WeeksToRace == weeksToRace);
            return (week.WeeksToRace, week.PlannedDistance, week.PlannedLongRun, string.Join(' ', week.RunningDays), week.RecoveryWeek);
        }

        public List<(int, double?, double?, string, bool)> Rows() => [.. Weeks.Select(w => Row(w.WeeksToRace))];
    }

    private sealed record PlannedWeekResponse(int WeeksToRace, double? PlannedDistance, double? PlannedLongRun, List<string> RunningDays, bool RecoveryWeek);
}
