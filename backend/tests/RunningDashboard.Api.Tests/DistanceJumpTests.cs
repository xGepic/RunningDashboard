using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Distance Jumps: a Training Week more than 10% above the week it is compared with, the previous one or, past
/// Recovery Weeks, the last one before them. Run weeks are compared with run weeks, on the Overview's weekly list and
/// the Training page; planned weeks with planned weeks of the same Training Plan. The first marathon
/// (Sun 27 Sep 2026) has a 12-week plan from Mon 6 Jul; week −11 is Mon 6 Jul, week −9 Mon 20 Jul, week −8 Mon 27 Jul,
/// week −7 Mon 3 Aug, week −6 Mon 10 Aug, week −3 Mon 31 Aug. The clock stands at Friday 9 Oct 2026, in the week of
/// Mon 5 Oct.
/// </summary>
public sealed class DistanceJumpTests
{
    private readonly TestApp _app = new();

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task A_week_12_5_percent_up_is_a_distance_jump_but_exactly_10_percent_up_is_not()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 9, 7), 40000),
            Run(2, new DateOnly(2026, 9, 14), 45000),
            Run(3, new DateOnly(2026, 9, 21), 49500));

        var overview = await GetOverviewAsync(browser);

        // The first week has nothing to compare with.
        Jump(overview, new DateOnly(2026, 9, 7)).ShouldBeNull();
        ShouldBeJump(Jump(overview, new DateOnly(2026, 9, 14)), new DateOnly(2026, 9, 7), 0.125);
        Jump(overview, new DateOnly(2026, 9, 21)).ShouldBeNull();
    }

    [TestCase(60000, 0.2)]
    [TestCase(54000, null)]
    public async Task A_week_after_a_recovery_week_is_compared_with_the_week_before_it(double distance, double? increase)
    {
        // 50 → 30 (Recovery Week) → 60 km is 20% up on 50 km; 54 km is only 8% up, though far above the 30 km.
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 7, 20), 50000),
            Run(2, new DateOnly(2026, 7, 27), 30000),
            Run(3, new DateOnly(2026, 8, 3), distance));
        await PlanFirstMarathonAsync(browser, recoveryWeeks: [-8]);

        var overview = await GetOverviewAsync(browser);

        if (increase is null)
            Jump(overview, new DateOnly(2026, 8, 3)).ShouldBeNull();
        else
            ShouldBeJump(Jump(overview, new DateOnly(2026, 8, 3)), new DateOnly(2026, 7, 20), increase.Value);
    }

    [Test]
    public async Task Two_recovery_weeks_in_a_row_are_skipped_together()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 7, 20), 50000),
            Run(2, new DateOnly(2026, 7, 27), 30000),
            Run(3, new DateOnly(2026, 8, 3), 25000),
            Run(4, new DateOnly(2026, 8, 10), 60000));
        await PlanFirstMarathonAsync(browser, recoveryWeeks: [-8, -7]);

        var overview = await GetOverviewAsync(browser);

        ShouldBeJump(Jump(overview, new DateOnly(2026, 8, 10)), new DateOnly(2026, 7, 20), 0.2);
    }

    [Test]
    public async Task There_is_no_distance_jump_after_a_week_without_runs()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 9, 7), 50000),
            // Ill in the week of 14 Sep.
            Run(2, new DateOnly(2026, 9, 21), 60000),
            Run(3, new DateOnly(2026, 9, 28), 70000));

        var overview = await GetOverviewAsync(browser);

        Jump(overview, new DateOnly(2026, 9, 21)).ShouldBeNull();
        // From there on, weeks are compared again.
        Jump(overview, new DateOnly(2026, 9, 28))!.ComparedWith.ShouldBe(new DateOnly(2026, 9, 21));
    }

    [Test]
    public async Task There_is_no_distance_jump_when_the_week_before_the_recovery_week_had_no_runs()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 7, 13), 50000),
            // Nothing in the week of 20 Jul.
            Run(2, new DateOnly(2026, 7, 27), 30000),
            Run(3, new DateOnly(2026, 8, 3), 60000));
        await PlanFirstMarathonAsync(browser, recoveryWeeks: [-8]);

        var overview = await GetOverviewAsync(browser);

        Jump(overview, new DateOnly(2026, 8, 3)).ShouldBeNull();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Outside_a_planned_block_a_normal_week_after_a_lighter_one_is_a_jump(bool inBlockWithoutPlan)
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 8, 31), 50000),
            Run(2, new DateOnly(2026, 9, 7), 30000),
            Run(3, new DateOnly(2026, 9, 14), 50000));
        if (inBlockWithoutPlan)
            await CreateFirstMarathonAsync(browser);

        var overview = await GetOverviewAsync(browser);

        ShouldBeJump(Jump(overview, new DateOnly(2026, 9, 14)), new DateOnly(2026, 9, 7), 2.0 / 3);
    }

    [Test]
    public async Task The_current_week_is_a_jump_as_soon_as_its_distance_so_far_passes_the_limit()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 9, 28), 40000),
            // Monday and Wednesday of the current week: 45 km by Friday, with the weekend still to come.
            new TestActivity(2, "Monday", new DateTime(2026, 10, 5, 18, 0, 0), 20000, 7200),
            new TestActivity(3, "Wednesday", new DateTime(2026, 10, 7, 18, 0, 0), 25000, 9000));

        var overview = await GetOverviewAsync(browser);

        ShouldBeJump(Jump(overview, new DateOnly(2026, 10, 5)), new DateOnly(2026, 9, 28), 0.125);
    }

    [Test]
    public async Task A_run_late_on_sunday_counts_in_its_own_week()
    {
        // 46 km up to Sunday 23:30, then 50 km: 8.7% up. Counted in the next week, it would be 36 → 60 km.
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Monday", new DateTime(2026, 9, 21, 18, 0, 0), 36000, 12960),
            new TestActivity(2, "Sunday night", new DateTime(2026, 9, 27, 23, 30, 0), 10000, 3600),
            new TestActivity(3, "Tuesday", new DateTime(2026, 9, 29, 18, 0, 0), 50000, 18000));

        var overview = await GetOverviewAsync(browser);

        Jump(overview, new DateOnly(2026, 9, 28)).ShouldBeNull();
    }

    [Test]
    public async Task Weeks_without_runs_have_no_distance_jump()
    {
        var browser = await _app.ConnectWithActivitiesAsync(Run(1, new DateOnly(2026, 9, 21), 40000));

        var overview = await GetOverviewAsync(browser);

        overview.Weeks.Select(w => w.DistanceJump).ShouldAllBe(j => j == null);
    }

    [Test]
    public async Task Each_week_of_a_training_block_carries_its_distance_jump_starting_with_the_week_before_the_block()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 6, 29), 40000),
            Run(2, new DateOnly(2026, 7, 6), 50000),
            Run(3, new DateOnly(2026, 7, 20), 50000),
            Run(4, new DateOnly(2026, 7, 27), 30000),
            Run(5, new DateOnly(2026, 8, 3), 60000));
        var race = await PlanFirstMarathonAsync(browser, recoveryWeeks: [-8]);

        var block = (await browser.GetFromJsonAsync<TrainingResponse>($"/api/training?goalRaceId={race}"))!.Block!;

        // Week −11, the block's first, against the week before the block.
        ShouldBeJump(ActualJump(block.Weeks, -11), new DateOnly(2026, 6, 29), 0.25);
        // Week −9 after a week without Runs, and the Recovery Week −8 well below it.
        ActualJump(block.Weeks, -9).ShouldBeNull();
        ActualJump(block.Weeks, -8).ShouldBeNull();
        // Week −7 past the plan's Recovery Week, against week −9.
        ShouldBeJump(ActualJump(block.Weeks, -7), new DateOnly(2026, 7, 20), 0.2);
    }

    [Test]
    public async Task A_past_block_in_the_comparison_carries_its_own_distance_jumps()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 6, 29), 40000),
            Run(2, new DateOnly(2026, 7, 6), 50000),
            Run(3, new DateOnly(2026, 7, 20), 50000),
            Run(4, new DateOnly(2026, 7, 27), 30000),
            Run(5, new DateOnly(2026, 8, 3), 60000));
        await PlanFirstMarathonAsync(browser, recoveryWeeks: [-8]);
        // The second marathon (Sun 23 May 2027): on Wed 10 Mar 2027 its block is shown, compared with the first's.
        (await browser.PostAsJsonAsync("/api/goal-races", new { name = "Second marathon", date = new DateOnly(2027, 5, 23), distance = 42195, trainingBlockWeeks = 16 }))
            .EnsureSuccessStatusCode();
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero));

        var comparison = (await browser.GetFromJsonAsync<TrainingResponse>("/api/training"))!.Block!.Comparison!;

        DistanceJumpResponse? ComparedJump(int weeksToRace) =>
            comparison.Weeks.Single(w => w.WeeksToRace == weeksToRace).Compared!.Actual!.DistanceJump;
        // The compared block's first week against the week before that block, and week −7 past its Recovery Week.
        ShouldBeJump(ComparedJump(-11), new DateOnly(2026, 6, 29), 0.25);
        ShouldBeJump(ComparedJump(-7), new DateOnly(2026, 7, 20), 0.2);
    }

    [Test]
    public async Task The_oldest_recent_week_of_the_countdown_is_compared_with_a_week_before_it_that_is_not_listed()
    {
        // The recent weeks are Mon 7 Sep to Mon 5 Oct. 7 Sep is compared with 24 Aug, past the Recovery Week of 31 Aug.
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 8, 24), 40000),
            Run(2, new DateOnly(2026, 8, 31), 20000),
            Run(3, new DateOnly(2026, 9, 7), 46000),
            Run(4, new DateOnly(2026, 9, 14), 46000),
            Run(5, new DateOnly(2026, 9, 21), 60000));
        await PlanFirstMarathonAsync(browser, recoveryWeeks: [-3]);
        (await browser.PostAsJsonAsync("/api/goal-races", new { name = "Second marathon", date = new DateOnly(2027, 5, 23), distance = 42195, trainingBlockWeeks = 16 }))
            .EnsureSuccessStatusCode();

        var countdown = (await browser.GetFromJsonAsync<TrainingResponse>("/api/training"))!.Countdown!;

        countdown.RecentWeeks.Select(w => w.Start).ShouldBe(
            [new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 5)]);
        ShouldBeJump(countdown.RecentWeeks[0].DistanceJump, new DateOnly(2026, 8, 24), 0.15);
        countdown.RecentWeeks[1].DistanceJump.ShouldBeNull();
        ShouldBeJump(countdown.RecentWeeks[2].DistanceJump, new DateOnly(2026, 9, 14), 60.0 / 46 - 1);
        countdown.RecentWeeks.Skip(3).ShouldAllBe(w => w.DistanceJump == null);
    }

    [Test]
    public async Task A_planned_week_12_5_percent_up_is_a_planned_distance_jump_but_the_first_plan_week_never_is()
    {
        var browser = _app.CreateBrowser();
        var race = await PlanFirstMarathonAsync(
            browser,
            new PlannedWeek(-11, 40000),
            new PlannedWeek(-10, 45000),
            new PlannedWeek(-9, 49500));

        var plan = await GetPlanAsync(browser, race);

        // Week −11 is not compared with the weeks before the plan.
        PlannedJump(plan, -11).ShouldBeNull();
        ShouldBeJump(PlannedJump(plan, -10), new DateOnly(2026, 7, 6), 0.125);
        // Exactly 10% up.
        PlannedJump(plan, -9).ShouldBeNull();
    }

    [Test]
    public async Task Saving_planned_weeks_returns_their_planned_distance_jumps()
    {
        var browser = _app.CreateBrowser();
        var race = await PlanFirstMarathonAsync(browser, new PlannedWeek(-11, 40000));

        var response = await browser.PutAsJsonAsync($"/api/goal-races/{race}/training-plan/weeks", new { weeks = new[] { new PlannedWeek(-10, 46000) } });
        var saved = (await response.Content.ReadFromJsonAsync<TrainingPlanResponse>())!;

        ShouldBeJump(PlannedJump(saved, -10), new DateOnly(2026, 7, 6), 0.15);
    }

    [Test]
    public async Task A_planned_week_next_to_one_without_a_planned_distance_has_no_planned_distance_jump()
    {
        var browser = _app.CreateBrowser();
        var race = await PlanFirstMarathonAsync(
            browser,
            new PlannedWeek(-11, 40000),
            new PlannedWeek(-10, null),
            new PlannedWeek(-9, 50000));

        var plan = await GetPlanAsync(browser, race);

        PlannedJump(plan, -10).ShouldBeNull();
        PlannedJump(plan, -9).ShouldBeNull();
    }

    [Test]
    public async Task A_planned_week_after_a_planned_recovery_week_is_compared_with_the_week_before_it()
    {
        var browser = _app.CreateBrowser();
        var race = await PlanFirstMarathonAsync(
            browser,
            new PlannedWeek(-9, 50000),
            new PlannedWeek(-8, 30000, RecoveryWeek: true),
            new PlannedWeek(-7, 60000),
            new PlannedWeek(-6, 54000),
            new PlannedWeek(-5, 30000, RecoveryWeek: true),
            new PlannedWeek(-4, 59000));

        var plan = await GetPlanAsync(browser, race);

        ShouldBeJump(PlannedJump(plan, -7), new DateOnly(2026, 7, 20), 0.2);
        PlannedJump(plan, -6).ShouldBeNull();
        // 59 km after the Recovery Week is less than 10% above the 54 km before it.
        PlannedJump(plan, -4).ShouldBeNull();
    }

    [Test]
    public async Task A_block_week_shows_planned_and_run_distance_jumps_apart()
    {
        // Week −7 was planned flat but run 25% up; week −6 was planned 20% up but run flat.
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 7, 20), 40000),
            Run(2, new DateOnly(2026, 7, 27), 30000),
            Run(3, new DateOnly(2026, 8, 3), 50000),
            Run(4, new DateOnly(2026, 8, 10), 50000));
        var race = await PlanFirstMarathonAsync(
            browser,
            new PlannedWeek(-9, 50000),
            new PlannedWeek(-8, 30000, RecoveryWeek: true),
            new PlannedWeek(-7, 50000),
            new PlannedWeek(-6, 60000));

        var block = (await browser.GetFromJsonAsync<TrainingResponse>($"/api/training?goalRaceId={race}"))!.Block!;

        var weekMinus7 = block.Weeks.Single(w => w.WeeksToRace == -7);
        weekMinus7.Plan!.DistanceJump.ShouldBeNull();
        ShouldBeJump(weekMinus7.Actual!.DistanceJump, new DateOnly(2026, 7, 20), 0.25);
        var weekMinus6 = block.Weeks.Single(w => w.WeeksToRace == -6);
        ShouldBeJump(weekMinus6.Plan!.DistanceJump, new DateOnly(2026, 8, 3), 0.2);
        weekMinus6.Actual!.DistanceJump.ShouldBeNull();
    }

    /// <summary>A Run on the Sunday of the Training Week starting <paramref name="monday"/>, at an easy 6:00 /km.</summary>
    private static TestActivity Run(long id, DateOnly monday, double distance) =>
        new(id, $"Long run of the week of {monday:d MMM}", monday.AddDays(6).ToDateTime(new TimeOnly(9, 0)), distance, (int)(distance * 0.36));

    private static async Task<int> CreateFirstMarathonAsync(HttpClient browser)
    {
        var response = await browser.PostAsJsonAsync("/api/goal-races", new { name = "Vienna Marathon", date = new DateOnly(2026, 9, 27), distance = 42195, trainingBlockWeeks = 14 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GoalRaceResponse>())!.Id;
    }

    /// <summary>
    /// Gives the first marathon a 12-week plan with <paramref name="recoveryWeeks"/> (Weeks to Race) as Recovery Weeks;
    /// returns its id.
    /// </summary>
    private static Task<int> PlanFirstMarathonAsync(HttpClient browser, int[] recoveryWeeks) =>
        PlanFirstMarathonAsync(browser, [.. recoveryWeeks.Select(n => new PlannedWeek(n, null, RecoveryWeek: true))]);

    /// <summary>Gives the first marathon a 12-week plan with <paramref name="weeks"/> planned; returns its id.</summary>
    private static async Task<int> PlanFirstMarathonAsync(HttpClient browser, params PlannedWeek[] weeks)
    {
        var id = await CreateFirstMarathonAsync(browser);
        (await browser.PutAsJsonAsync($"/api/goal-races/{id}/training-plan", new { length = 12, phases = new[] { "build", "peak", "taper" } }))
            .EnsureSuccessStatusCode();
        (await browser.PutAsJsonAsync($"/api/goal-races/{id}/training-plan/weeks", new { weeks })).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<TrainingPlanResponse> GetPlanAsync(HttpClient browser, int race) =>
        (await browser.GetFromJsonAsync<TrainingPlanResponse>($"/api/goal-races/{race}/training-plan"))!;

    private static DistanceJumpResponse? PlannedJump(TrainingPlanResponse plan, int weeksToRace) =>
        plan.Weeks.Single(w => w.WeeksToRace == weeksToRace).DistanceJump;

    private static DistanceJumpResponse? ActualJump(List<BlockWeekResponse> weeks, int weeksToRace) =>
        weeks.Single(w => w.WeeksToRace == weeksToRace).Actual!.DistanceJump;

    private static DistanceJumpResponse? Jump(OverviewResponse overview, DateOnly monday) =>
        overview.Weeks.Where(w => w.Start == monday).ShouldHaveSingleItem().DistanceJump;

    /// <summary>Checks a Distance Jump; the increase is a ratio, so it is compared within rounding.</summary>
    private static void ShouldBeJump(DistanceJumpResponse? jump, DateOnly comparedWith, double increase)
    {
        jump.ShouldNotBeNull();
        jump.ComparedWith.ShouldBe(comparedWith);
        jump.Increase.ShouldBe(increase, 1e-9);
    }

    private static async Task<OverviewResponse> GetOverviewAsync(HttpClient browser) =>
        (await browser.GetFromJsonAsync<OverviewResponse>("/api/overview"))!;

    private sealed record GoalRaceResponse(int Id);

    /// <param name="PlannedDistance">Metres, or none.</param>
    private sealed record PlannedWeek(int WeeksToRace, double? PlannedDistance, bool RecoveryWeek = false);

    private sealed record TrainingPlanResponse(List<PlanWeekResponse> Weeks);

    /// <param name="DistanceJump">None unless the planned week is one.</param>
    private sealed record PlanWeekResponse(int WeeksToRace, double? PlannedDistance, DistanceJumpResponse? DistanceJump);

    private sealed record TrainingResponse(BlockResponse? Block, CountdownResponse? Countdown);

    private sealed record BlockResponse(List<BlockWeekResponse> Weeks, ComparisonResponse? Comparison);

    private sealed record ComparisonResponse(List<LinedUpWeekResponse> Weeks);

    private sealed record LinedUpWeekResponse(int WeeksToRace, BlockWeekResponse? Viewed, BlockWeekResponse? Compared);

    /// <param name="Actual">What was run; none for a future week.</param>
    /// <param name="Plan">What the Training Plan has for the week; none without a plan.</param>
    private sealed record BlockWeekResponse(int WeeksToRace, ActualResponse? Actual, BlockPlanResponse? Plan);

    /// <param name="DistanceJump">None unless the planned week is one, against the planned week before it.</param>
    private sealed record BlockPlanResponse(double? PlannedDistance, DistanceJumpResponse? DistanceJump);

    /// <param name="DistanceJump">None unless the week is one.</param>
    private sealed record ActualResponse(double Distance, DistanceJumpResponse? DistanceJump);

    /// <param name="RecentWeeks">Oldest first.</param>
    private sealed record CountdownResponse(List<RecentWeekResponse> RecentWeeks);

    /// <param name="DistanceJump">None unless the week is one.</param>
    private sealed record RecentWeekResponse(DateOnly Start, double Distance, DistanceJumpResponse? DistanceJump);

    private sealed record OverviewResponse(List<WeekResponse> Weeks);

    /// <param name="DistanceJump">None unless the week is one.</param>
    private sealed record WeekResponse(DateOnly Start, double Distance, DistanceJumpResponse? DistanceJump);

    /// <param name="ComparedWith">The Monday of the week compared with.</param>
    /// <param name="Increase">The ratio of the distances minus one: 0.2 for 20% up.</param>
    private sealed record DistanceJumpResponse(DateOnly ComparedWith, double Increase);
}
