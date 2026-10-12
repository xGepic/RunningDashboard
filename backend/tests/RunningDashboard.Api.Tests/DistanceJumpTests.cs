using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Distance Jumps on the Overview's weekly list: a Training Week more than 10% above the week it is compared with,
/// the previous one or, past Recovery Weeks, the last one before them. The first marathon (Sun 27 Sep 2026) has a
/// 12-week plan; week −9 is Mon 20 Jul, week −8 Mon 27 Jul, week −7 Mon 3 Aug. The clock stands at Friday 9 Oct 2026,
/// in the week of Mon 5 Oct.
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

    /// <summary>A Run on the Sunday of the Training Week starting <paramref name="monday"/>, at an easy 6:00 /km.</summary>
    private static TestActivity Run(long id, DateOnly monday, double distance) =>
        new(id, $"Long run of the week of {monday:d MMM}", monday.AddDays(6).ToDateTime(new TimeOnly(9, 0)), distance, (int)(distance * 0.36));

    private static async Task<int> CreateFirstMarathonAsync(HttpClient browser)
    {
        var response = await browser.PostAsJsonAsync("/api/goal-races", new { name = "Vienna Marathon", date = new DateOnly(2026, 9, 27), distance = 42195, trainingBlockWeeks = 14 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GoalRaceResponse>())!.Id;
    }

    /// <summary>Gives the first marathon a 12-week plan with <paramref name="recoveryWeeks"/> (Weeks to Race) as Recovery Weeks.</summary>
    private static async Task PlanFirstMarathonAsync(HttpClient browser, int[] recoveryWeeks)
    {
        var id = await CreateFirstMarathonAsync(browser);
        (await browser.PutAsJsonAsync($"/api/goal-races/{id}/training-plan", new { length = 12, phases = new[] { "build", "peak", "taper" } }))
            .EnsureSuccessStatusCode();
        var weeks = recoveryWeeks.Select(n => new { weeksToRace = n, recoveryWeek = true });
        (await browser.PutAsJsonAsync($"/api/goal-races/{id}/training-plan/weeks", new { weeks })).EnsureSuccessStatusCode();
    }

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

    private sealed record OverviewResponse(List<WeekResponse> Weeks);

    /// <param name="DistanceJump">None unless the week is one.</param>
    private sealed record WeekResponse(DateOnly Start, double Distance, DistanceJumpResponse? DistanceJump);

    /// <param name="ComparedWith">The Monday of the week compared with.</param>
    /// <param name="Increase">The ratio of the distances minus one: 0.2 for 20% up.</param>
    private sealed record DistanceJumpResponse(DateOnly ComparedWith, double Increase);
}
