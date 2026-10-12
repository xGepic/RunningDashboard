using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// A Goal Race's Training Plan: its length (12, 16 or 20 weeks) and the type of each 4-week Phase. The second
/// marathon is on Sun 23 May 2027, so its Race Week starts Mon 17 May; on its own it has a 10-week block.
/// </summary>
public sealed class TrainingPlanTests
{
    private const double Marathon = 42195;

    private static readonly GoalRaceRequest FirstMarathon = new("Vienna Marathon", new DateOnly(2026, 9, 27), Marathon, null, 14);

    private static readonly GoalRaceRequest SecondMarathon = new("Second marathon", new DateOnly(2027, 5, 23), Marathon, 5 * 3600, 10);

    private static readonly TrainingPlanRequest BuildPeakPeakTaper = new(16, ["build", "peak", "peak", "taper"]);

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
    public async Task A_16_week_build_peak_peak_taper_plan_splits_into_phases_and_sets_the_training_block()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);

        var saved = await PutPlanAsync(race, BuildPeakPeakTaper);
        var plan = await GetPlanAsync(race);
        var listed = (await ListGoalRacesAsync()).ShouldHaveSingleItem();

        plan.ShouldBe(saved);
        plan.Length.ShouldBe(16);
        plan.Phases.ShouldBe(
            [new PhaseResponse("build", -15, -12), new PhaseResponse("peak", -11, -8), new PhaseResponse("peak", -7, -4), new PhaseResponse("taper", -3, 0)]);
        plan.Weeks.Select(w => w.WeeksToRace).ShouldBe(Enumerable.Range(-15, 16));
        plan.Weeks[0].ShouldBe(new WeekResponse(-15, new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 7), "build"));
        plan.Weeks[^1].ShouldBe(new WeekResponse(0, new DateOnly(2027, 5, 17), new DateOnly(2027, 5, 23), "taper"));
        // The plan's 16 weeks are the Training Block now; the race's own 10 weeks only count without a plan.
        (listed.TrainingBlockStart, listed.TrainingPlanLength, listed.TrainingBlockWeeks).ShouldBe((new DateOnly(2027, 2, 1), 16, 10));
    }

    [TestCase(14, new[] { "build", "peak", "taper" }, "length")]
    [TestCase(null, new[] { "build", "peak", "peak", "taper" }, "length")]
    [TestCase(16, new[] { "build", "peak", "taper", "peak" }, "phases")]
    [TestCase(16, new[] { "build", "peak", "taper" }, "phases")]
    [TestCase(16, new[] { "build", "peak", "recovery", "taper" }, "phases")]
    [TestCase(16, new[] { "build", "7", "peak", "taper" }, "phases")]
    public async Task A_plan_must_be_12_16_or_20_weeks_of_4_week_phases_ending_with_a_taper(int? length, string[] phases, string field)
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);

        var response = await _browser.PutAsJsonAsync(PlanPath(race), new TrainingPlanRequest(length, phases));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem!.Errors.Keys.ShouldBe([field]);
        (await _browser.GetAsync(PlanPath(race))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task The_last_phase_must_be_a_taper()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);

        var response = await _browser.PutAsJsonAsync(PlanPath(race), new TrainingPlanRequest(12, ["build", "peak", "peak"]));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        problem!.Errors["phases"].ShouldHaveSingleItem().ShouldBe("The last Phase must be a Taper.");
    }

    [Test]
    public async Task Moving_the_race_date_moves_the_plan_with_it()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);
        await PutWeeksAsync(race, new WeekRequest(-15, 30000, 12000, Days("tuesday easy, thursday tempo, sunday long"), false));

        var moved = await _browser.PutAsJsonAsync($"/api/goal-races/{race.Id}", SecondMarathon with { Date = new DateOnly(2027, 5, 30) });
        var plan = await GetPlanAsync(race);

        moved.StatusCode.ShouldBe(HttpStatusCode.OK);
        plan.Weeks[0].ShouldBe(new WeekResponse(-15, new DateOnly(2027, 2, 8), new DateOnly(2027, 2, 14), "build"));
        plan.Weeks[^1].ShouldBe(new WeekResponse(0, new DateOnly(2027, 5, 24), new DateOnly(2027, 5, 30), "taper"));
        (await ListGoalRacesAsync()).ShouldHaveSingleItem().TrainingBlockStart.ShouldBe(new DateOnly(2027, 2, 8));
        // The Run Types stay with week −15, now Mon 8 to Sun 14 Feb.
        (await GetPlannedWeeksAsync(race)).Row(-15).ShouldBe((-15, 30000.0, 12000.0, "tuesday easy, thursday tempo, sunday long", false));
    }

    [Test]
    public async Task Changing_the_length_keeps_the_weeks_still_in_range_and_adds_empty_ones()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);
        await PutWeeksAsync(race, new WeekRequest(-15, 30000, 12000, Days("sunday long"), false), new WeekRequest(-11, 35000, 14000, Days("tuesday tempo, sunday long"), true));

        var shorter = await PutPlanAsync(race, new TrainingPlanRequest(12, ["build", "peak", "taper"]));
        var longer = await PutPlanAsync(race, new TrainingPlanRequest(20, ["build", "build", "peak", "peak", "taper"]));
        var weeks = await GetPlannedWeeksAsync(race);

        shorter.Weeks.Select(w => w.WeeksToRace).ShouldBe(Enumerable.Range(-11, 12));
        shorter.Weeks.Take(4).Select(w => w.Phase).ShouldBe(["build", "build", "build", "build"]);
        longer.Weeks.Select(w => w.WeeksToRace).ShouldBe(Enumerable.Range(-19, 20));
        longer.Weeks[0].Start.ShouldBe(new DateOnly(2027, 1, 4));
        (await ListGoalRacesAsync()).ShouldHaveSingleItem().TrainingBlockStart.ShouldBe(new DateOnly(2027, 1, 4));
        // Week −11 stayed in range throughout; week −15 was dropped by the 12-week plan and came back empty.
        weeks.Row(-11).ShouldBe((-11, 35000.0, 14000.0, "tuesday tempo, sunday long", true));
        weeks.Row(-15).ShouldBe((-15, null, null, "", false));
    }

    [Test]
    public async Task Weekly_rows_are_saved_and_read_back()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);

        var response = await _browser.PutAsJsonAsync(WeeksPath(race), new TrainingPlanWeeksRequest(
        [
            new WeekRequest(-15, 30000, 12000, Days("tuesday easy, thursday tempo, saturday easy, sunday long"), false),
            new WeekRequest(-12, 25000, 10000, Days("sunday long, tuesday intervals"), true),
            new WeekRequest(0, 55000, 42195, Days("tuesday easy, thursday easy, sunday race"), false),
        ]));
        var saved = await response.Content.ReadFromJsonAsync<PlannedWeeksResponse>();
        var read = await GetPlannedWeeksAsync(race);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        read.Rows().ShouldBe(saved!.Rows());
        read.Weeks.Count.ShouldBe(16);
        read.Row(-15).ShouldBe((-15, 30000.0, 12000.0, "tuesday easy, thursday tempo, saturday easy, sunday long", false));
        // Running Days come back Monday first.
        read.Row(-12).ShouldBe((-12, 25000.0, 10000.0, "tuesday intervals, sunday long", true));
        read.Row(0).ShouldBe((0, 55000.0, 42195.0, "tuesday easy, thursday easy, sunday race", false));
        // Weeks left out keep what they had: nothing yet.
        read.Row(-14).ShouldBe((-14, null, null, "", false));
    }

    [Test]
    public async Task Days_left_out_are_rest_days_and_several_days_can_have_the_same_run_type()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);

        // Two Long days and no planned Long Run: Run Types are not checked against the Weekly Target.
        await PutWeeksAsync(race, new WeekRequest(-14, 32000, null, Days("sunday long, saturday long, wednesday easy"), false));

        var week = (await GetPlannedWeeksAsync(race)).Weeks.Single(w => w.WeeksToRace == -14);
        week.RunningDays.ShouldBe([new RunningDayResponse("wednesday", "easy"), new RunningDayResponse("saturday", "long"), new RunningDayResponse("sunday", "long")]);
    }

    [Test]
    public async Task Saving_weeks_again_replaces_what_was_planned_for_them()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);
        await PutWeeksAsync(race, new WeekRequest(-15, 30000, 12000, Days("tuesday easy, sunday long"), true));

        await PutWeeksAsync(race, new WeekRequest(-15, null, null, [], false));

        (await GetPlannedWeeksAsync(race)).Row(-15).ShouldBe((-15, null, null, "", false));
    }

    [Test]
    public async Task The_race_week_cannot_be_a_recovery_week()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);

        var response = await _browser.PutAsJsonAsync(WeeksPath(race), new TrainingPlanWeeksRequest(
        [
            new WeekRequest(-1, 40000, 16000, Days("tuesday easy, sunday long"), false),
            new WeekRequest(0, 55000, 42195, Days("sunday race"), true),
        ]));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem!.Errors["weeks[1].recoveryWeek"].ShouldHaveSingleItem().ShouldBe("The Race Week can't be a Recovery Week.");
        // Nothing is saved, not even the valid week.
        (await GetPlannedWeeksAsync(race)).Row(-1).ShouldBe((-1, null, null, "", false));
    }

    [TestCase(-16, 30000.0, 12000.0, "tuesday easy", "weeks[0].weeksToRace")]
    [TestCase(1, 30000.0, 12000.0, "tuesday easy", "weeks[0].weeksToRace")]
    [TestCase(-15, 0.0, 12000.0, "tuesday easy", "weeks[0].plannedDistance")]
    [TestCase(-15, 30000.0, -1.0, "tuesday easy", "weeks[0].plannedLongRun")]
    [TestCase(-15, 30000.0, 12000.0, "funday easy", "weeks[0].runningDays")]
    [TestCase(-15, 30000.0, 12000.0, "3 easy", "weeks[0].runningDays")]
    [TestCase(-15, 30000.0, 12000.0, "tuesday jog", "weeks[0].runningDays")]
    [TestCase(-15, 30000.0, 12000.0, "tuesday 0", "weeks[0].runningDays")]
    [TestCase(-15, 30000.0, 12000.0, "tuesday", "weeks[0].runningDays")]
    [TestCase(-15, 30000.0, 12000.0, "tuesday easy, sunday long, tuesday tempo", "weeks[0].runningDays")]
    public async Task A_week_must_be_in_the_plan_with_positive_distances_and_one_known_run_type_per_real_day(
        int weeksToRace, double distance, double longRun, string runningDays, string field)
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);

        var response = await _browser.PutAsJsonAsync(WeeksPath(race), new TrainingPlanWeeksRequest([new WeekRequest(weeksToRace, distance, longRun, Days(runningDays), false)]));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem!.Errors.Keys.ShouldBe([field]);
    }

    [Test]
    public async Task A_week_can_be_in_a_request_only_once()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);

        var response = await _browser.PutAsJsonAsync(WeeksPath(race), new TrainingPlanWeeksRequest(
            [new WeekRequest(-3, 30000, 12000, [], false), new WeekRequest(-3, 20000, 8000, [], false)]));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem!.Errors.Keys.ShouldBe(["weeks[1].weeksToRace"]);
    }

    [Test]
    public async Task Weeks_of_a_goal_race_without_a_plan_cannot_be_saved()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);

        var response = await _browser.PutAsJsonAsync(WeeksPath(race), new TrainingPlanWeeksRequest([new WeekRequest(0, 55000, 42195, [], false)]));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_plan_whose_block_would_overlap_another_goal_races_is_rejected()
    {
        await CreateGoalRaceAsync(FirstMarathon);
        // Sun 3 Jan 2027: 4 weeks from Mon 7 Dec 2026 are fine, 20 weeks from Mon 17 Aug 2026 overlap the first marathon.
        var race = await CreateGoalRaceAsync(new GoalRaceRequest("New Year half", new DateOnly(2027, 1, 3), 21097.5, null, 4));

        var response = await _browser.PutAsJsonAsync(PlanPath(race), new TrainingPlanRequest(20, ["build", "build", "peak", "peak", "taper"]));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem!.Errors["length"].ShouldHaveSingleItem().ShouldBe(
            "The Training Block (17 Aug 2026 – 3 Jan 2027) overlaps the one of Vienna Marathon (22 Jun – 27 Sep 2026).");
    }

    [Test]
    public async Task Another_goal_race_inside_a_planned_block_is_rejected_even_outside_the_races_own_block_length()
    {
        var second = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(second, BuildPeakPeakTaper);

        // Sun 14 Feb 2027, 2 weeks: clear of the race's own 10 weeks (from Mon 15 Mar), inside the plan's 16 (from Mon 1 Feb).
        var tuneUp = await _browser.PostAsJsonAsync("/api/goal-races", new GoalRaceRequest("Tune-up 10K", new DateOnly(2027, 2, 14), 10000, null, 2));
        // Lengthening the planned race's own block changes nothing while it has a plan.
        var edited = await _browser.PutAsJsonAsync($"/api/goal-races/{second.Id}", SecondMarathon with { TrainingBlockWeeks = 30 });

        tuneUp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Deleting_the_plan_gives_the_race_back_its_own_block_length()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);
        await PutWeeksAsync(race, new WeekRequest(0, 55000, 42195, Days("sunday race"), false));

        var deleted = await _browser.DeleteAsync(PlanPath(race));
        var again = await _browser.DeleteAsync(PlanPath(race));
        var listed = (await ListGoalRacesAsync()).ShouldHaveSingleItem();

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _browser.GetAsync(PlanPath(race))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        // Its own 10 weeks: from Mon 15 Mar 2027 instead of the plan's Mon 1 Feb.
        (listed.TrainingBlockStart, listed.TrainingPlanLength).ShouldBe((new DateOnly(2027, 3, 15), null));
        // A new plan starts from nothing.
        await PutPlanAsync(race, BuildPeakPeakTaper);
        (await GetPlannedWeeksAsync(race)).Row(0).ShouldBe((0, null, null, "", false));
    }

    [Test]
    public async Task Deleting_a_goal_race_deletes_its_plan()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);
        await PutPlanAsync(race, BuildPeakPeakTaper);

        var deleted = await _browser.DeleteAsync($"/api/goal-races/{race.Id}");
        var again = await CreateGoalRaceAsync(SecondMarathon);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _browser.GetAsync(PlanPath(again))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListGoalRacesAsync()).ShouldHaveSingleItem().TrainingBlockStart.ShouldBe(new DateOnly(2027, 3, 15));
    }

    [Test]
    public async Task A_goal_race_without_a_plan_or_an_unknown_one_has_none_to_show()
    {
        var race = await CreateGoalRaceAsync(SecondMarathon);

        (await _browser.GetAsync(PlanPath(race))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _browser.PutAsJsonAsync("/api/goal-races/999/training-plan", BuildPeakPeakTaper)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<TrainingPlanResponse>())!;
    }

    private async Task<TrainingPlanResponse> GetPlanAsync(GoalRaceResponse race) =>
        (await _browser.GetFromJsonAsync<TrainingPlanResponse>(PlanPath(race)))!;

    private static string WeeksPath(GoalRaceResponse race) => $"{PlanPath(race)}/weeks";

    private async Task PutWeeksAsync(GoalRaceResponse race, params WeekRequest[] weeks)
    {
        var response = await _browser.PutAsJsonAsync(WeeksPath(race), new TrainingPlanWeeksRequest([.. weeks]));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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
    /// <param name="RunningDays">In any order; the days left out are rest days.</param>
    private sealed record WeekRequest(int WeeksToRace, double? PlannedDistance, double? PlannedLongRun, RunningDayRequest[] RunningDays, bool RecoveryWeek);

    /// <param name="Day">"monday" to "sunday".</param>
    /// <param name="RunType">"easy", "long", "tempo", "intervals" or "race".</param>
    private sealed record RunningDayRequest(string Day, string? RunType);

    /// <summary>Running Days written as "tuesday easy, sunday long"; a day written without a Run Type is sent without one.</summary>
    private static RunningDayRequest[] Days(string days) =>
        [.. days.Split(", ").Select(d => d.Split(' ')).Select(d => new RunningDayRequest(d[0], d.ElementAtOrDefault(1)))];

    private sealed record PlannedWeeksResponse(List<PlannedWeekResponse> Weeks)
    {
        /// <summary>What is planned for one week, comparable as a whole: Running Days written as "tuesday easy, sunday long".</summary>
        public (int, double?, double?, string, bool) Row(int weeksToRace)
        {
            var week = Weeks.Single(w => w.WeeksToRace == weeksToRace);
            var days = string.Join(", ", week.RunningDays.Select(d => $"{d.Day} {d.RunType}"));
            return (week.WeeksToRace, week.PlannedDistance, week.PlannedLongRun, days, week.RecoveryWeek);
        }

        public List<(int, double?, double?, string, bool)> Rows() => [.. Weeks.Select(w => Row(w.WeeksToRace))];
    }

    /// <param name="RunningDays">Monday first.</param>
    private sealed record PlannedWeekResponse(int WeeksToRace, double? PlannedDistance, double? PlannedLongRun, List<RunningDayResponse> RunningDays, bool RecoveryWeek);

    /// <param name="Day">"monday" to "sunday".</param>
    /// <param name="RunType">"easy", "long", "tempo", "intervals" or "race".</param>
    private sealed record RunningDayResponse(string Day, string RunType);
}
