using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// The Training page's block view against the Training Plan. The second marathon (Sun 23 May 2027) has a 16-week
/// Build–Peak–Peak–Taper plan from Mon 1 Feb; most planned weeks run on Tuesday, Thursday and Sunday. The clock
/// stands at Wednesday 10 Mar 2027, in week −10 (Mon 8 to Sun 14 Mar).
/// </summary>
public sealed class TrainingPlanVsActualTests
{
    private const double Marathon = 42195;

    private static readonly GoalRaceRequest SecondMarathon = new("Second marathon", new DateOnly(2027, 5, 23), Marathon, 5 * 3600, 10);

    private static readonly string[] TuesdayThursdaySunday = ["tuesday", "thursday", "sunday"];
    private static readonly string[] BuildPeakPeakTaper = ["build", "peak", "peak", "taper"];

    private readonly TestApp _app = new();


    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task A_week_where_the_plan_was_met()
    {
        // Week −11, Mon 1 to Sun 7 Mar 2027, the first Peak week: 30 km with a 16 km Long Run planned.
        var block = await PlannedBlockAsync(
            [
                Run(1, "Tuesday easy", new DateTime(2027, 3, 2, 18, 0, 0), 10000),
                Run(2, "Thursday tempo", new DateTime(2027, 3, 4, 18, 0, 0), 6000),
                Run(3, "Sunday long run", new DateTime(2027, 3, 7, 8, 0, 0), 16000),
            ],
            new WeekRequest(-11, 30000, 16000, TuesdayThursdaySunday, false));

        var plan = Week(block, -11).Plan!;

        (plan.Phase, plan.RecoveryWeek, plan.PlannedDistance, plan.PlannedLongRun).ShouldBe(("peak", false, 30000.0, 16000.0));
        (plan.DistanceMet, plan.LongRunMet).ShouldBe((true, true));
        plan.Days.Select(d => d.State).ShouldBe(["rest", "ran", "rest", "ran", "rest", "rest", "ran"]);
        plan.Days[0].Date.ShouldBe(new DateOnly(2027, 3, 1));
        plan.Days[^1].Date.ShouldBe(new DateOnly(2027, 3, 7));
        Week(block, -11).Actual!.Distance.ShouldBe(32000);
    }

    [Test]
    public async Task A_missed_running_day_and_a_run_on_an_unplanned_day()
    {
        // Week −12, Mon 22 to Sun 28 Feb 2027: Thursday and Sunday were skipped, the Long Run moved to Saturday.
        var block = await PlannedBlockAsync(
            [
                Run(1, "Tuesday easy", new DateTime(2027, 2, 23, 18, 0, 0), 8000),
                Run(2, "Saturday long run", new DateTime(2027, 2, 27, 8, 0, 0), 14000),
            ],
            new WeekRequest(-12, 30000, 15000, TuesdayThursdaySunday, false));

        var plan = Week(block, -12).Plan!;

        plan.Days.Select(d => d.State).ShouldBe(["rest", "ran", "rest", "missed", "rest", "unplanned", "missed"]);
        (plan.DistanceMet, plan.LongRunMet).ShouldBe((false, false));
    }

    [Test]
    public async Task Planned_days_of_the_current_week_still_to_come_are_not_missed()
    {
        // Week −10, Mon 8 to Sun 14 Mar 2027, today Wednesday: Tuesday was run, Thursday and Sunday are ahead.
        var block = await PlannedBlockAsync(
            [Run(1, "Tuesday easy", new DateTime(2027, 3, 9, 18, 0, 0), 8000)],
            new WeekRequest(-10, 30000, 16000, TuesdayThursdaySunday, false));

        var week = Week(block, -10);

        week.Status.ShouldBe("current");
        week.Plan!.Days.Select(d => d.State).ShouldBe(["rest", "ran", "rest", "planned", "rest", "rest", "planned"]);
        // 8 of 30 km so far: not short yet, the week is still running.
        (week.Plan.DistanceMet, week.Plan.LongRunMet).ShouldBe(((bool?)null, (bool?)null));
    }

    [Test]
    public async Task A_future_week_shows_only_the_plan()
    {
        // Week −9, Mon 15 to Sun 21 Mar 2027: a Recovery Week.
        var block = await PlannedBlockAsync([], new WeekRequest(-9, 24000, 12000, ["tuesday", "sunday"], true));

        var week = Week(block, -9);

        (week.Status, week.Actual).ShouldBe(("future", null));
        (week.Plan!.Phase, week.Plan.RecoveryWeek, week.Plan.PlannedDistance, week.Plan.PlannedLongRun).ShouldBe(("peak", true, 24000.0, 12000.0));
        (week.Plan.DistanceMet, week.Plan.LongRunMet).ShouldBe(((bool?)null, (bool?)null));
        week.Plan.Days.Select(d => d.State).ShouldBe(["rest", "planned", "rest", "rest", "rest", "rest", "planned"]);
    }

    [Test]
    public async Task A_week_without_targets_has_nothing_to_meet()
    {
        var block = await PlannedBlockAsync([Run(1, "Tuesday easy", new DateTime(2027, 3, 2, 18, 0, 0), 8000)]);

        var plan = Week(block, -11).Plan!;

        (plan.PlannedDistance, plan.PlannedLongRun, plan.DistanceMet, plan.LongRunMet).ShouldBe(((double?)null, (double?)null, (bool?)null, (bool?)null));
        plan.Days.Select(d => d.State).ShouldBe(["rest", "unplanned", "rest", "rest", "rest", "rest", "rest"]);
    }

    [Test]
    public async Task The_block_shows_its_phases_as_bands()
    {
        var block = await PlannedBlockAsync([]);

        block.Phases.ShouldBe(
            [new PhaseResponse("build", -15, -12), new PhaseResponse("peak", -11, -8), new PhaseResponse("peak", -7, -4), new PhaseResponse("taper", -3, 0)]);
        Week(block, 0).Plan!.Phase.ShouldBe("taper");
    }

    [Test]
    public async Task A_block_without_a_plan_shows_actual_values_only()
    {
        var browser = await _app.ConnectWithActivitiesAsync(Run(1, "Tuesday easy", new DateTime(2027, 3, 2, 18, 0, 0), 8000));
        // Its own 16 weeks, as long as the plan would be.
        await CreateGoalRaceAsync(browser, SecondMarathon with { TrainingBlockWeeks = 16 });
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero));

        var block = (await browser.GetFromJsonAsync<TrainingResponse>("/api/training"))!.Block!;

        block.Phases.ShouldBeNull();
        block.Weeks.ShouldAllBe(w => w.Plan == null);
        Week(block, -11).Actual!.Distance.ShouldBe(8000);
    }

    /// <summary>The second marathon's planned block on Wed 10 Mar 2027, with <paramref name="runs"/> and <paramref name="weeks"/> planned.</summary>
    private async Task<BlockResponse> PlannedBlockAsync(TestActivity[] runs, params WeekRequest[] weeks)
    {
        var browser = await _app.ConnectWithActivitiesAsync(runs);
        var race = await CreateGoalRaceAsync(browser, SecondMarathon);
        (await browser.PutAsJsonAsync($"/api/goal-races/{race.Id}/training-plan", new { length = 16, phases = BuildPeakPeakTaper }))
            .EnsureSuccessStatusCode();
        (await browser.PutAsJsonAsync($"/api/goal-races/{race.Id}/training-plan/weeks", new { weeks }))
            .EnsureSuccessStatusCode();
        _app.Clock.SetUtcNow(new DateTimeOffset(2027, 3, 10, 12, 0, 0, TimeSpan.Zero));

        return (await browser.GetFromJsonAsync<TrainingResponse>("/api/training"))!.Block!;
    }

    /// <summary>A Run in Vienna in winter, at an easy 6:00 /km.</summary>
    private static TestActivity Run(long id, string name, DateTime startLocal, double distance) =>
        new(id, name, startLocal, distance, (int)(distance * 0.36), UtcOffsetHours: 1);

    private static WeekResponse Week(BlockResponse block, int weeksToRace) => block.Weeks.Single(w => w.WeeksToRace == weeksToRace);

    private static async Task<GoalRaceResponse> CreateGoalRaceAsync(HttpClient browser, GoalRaceRequest race)
    {
        var response = await browser.PostAsJsonAsync("/api/goal-races", race);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GoalRaceResponse>())!;
    }

    /// <param name="TargetTime">Seconds.</param>
    private sealed record GoalRaceRequest(string Name, DateOnly Date, double Distance, int? TargetTime, int TrainingBlockWeeks);

    private sealed record GoalRaceResponse(int Id);

    /// <param name="PlannedDistance">Metres.</param>
    /// <param name="PlannedLongRun">Metres.</param>
    private sealed record WeekRequest(int WeeksToRace, double? PlannedDistance, double? PlannedLongRun, string[] RunningDays, bool RecoveryWeek);

    private sealed record TrainingResponse(BlockResponse? Block);

    /// <param name="Phases">None without a Training Plan.</param>
    private sealed record BlockResponse(List<PhaseResponse>? Phases, List<WeekResponse> Weeks);

    private sealed record PhaseResponse(string Type, int FirstWeeksToRace, int LastWeeksToRace);

    /// <param name="Plan">None without a Training Plan.</param>
    private sealed record WeekResponse(int WeeksToRace, string Status, ActualResponse? Actual, PlanResponse? Plan);

    /// <param name="Distance">Metres.</param>
    private sealed record ActualResponse(double Distance);

    /// <param name="DistanceMet">None while the week runs short of it, for a future week, or without a planned distance.</param>
    /// <param name="Days">Monday to Sunday.</param>
    private sealed record PlanResponse(
        string Phase,
        bool RecoveryWeek,
        double? PlannedDistance,
        double? PlannedLongRun,
        bool? DistanceMet,
        bool? LongRunMet,
        List<DayResponse> Days);

    /// <param name="State">"ran", "missed", "unplanned", "planned" (still to come) or "rest".</param>
    private sealed record DayResponse(DateOnly Date, string State);
}
