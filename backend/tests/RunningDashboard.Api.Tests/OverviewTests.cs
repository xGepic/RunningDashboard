using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>The clock stands at Friday 2026-10-09, so the current Training Week starts Monday 2026-10-05.</summary>
public sealed class OverviewTests
{
    private readonly TestApp _app = new();


    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Totals_add_up_all_runs_and_each_calendar_year_by_local_time()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "New Year's Eve run", new DateTime(2025, 12, 31, 15, 0, 0), 5000, 1800, UtcOffsetHours: 1, ElevationGain: 20),
            // 2025-12-31T23:30Z in UTC, but 2026 where it was run.
            new TestActivity(2, "New Year's run", new DateTime(2026, 1, 1, 0, 30, 0), 10000, 3600, UtcOffsetHours: 1, ElevationGain: 50),
            new TestActivity(3, "Spring run", new DateTime(2026, 3, 10, 18, 0, 0), 8000, 3000, UtcOffsetHours: 1, ElevationGain: 30));

        var overview = await browser.GetOverviewAsync();

        overview.Totals.ShouldBe(new TotalsResponse(3, 23000, 8400, 100));
        overview.Years.ShouldBe(
            [new YearResponse(2025, 1, 5000, 1800, 20), new YearResponse(2026, 2, 18000, 6600, 80)]);
        // No Run on 9 Oct 2025.
        overview.OnThisDay.ShouldBeNull();
    }

    [Test]
    public async Task A_run_late_on_sunday_counts_in_the_week_that_started_on_monday()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Sunday night", new DateTime(2026, 9, 27, 23, 30, 0), 10000, 3600),
            new TestActivity(2, "Monday just after midnight", new DateTime(2026, 9, 28, 0, 10, 0), 5000, 1800));

        var overview = await browser.GetOverviewAsync();

        DistanceInWeek(overview, new DateOnly(2026, 9, 21)).ShouldBe(10000);
        DistanceInWeek(overview, new DateOnly(2026, 9, 28)).ShouldBe(5000);
    }

    [Test]
    public async Task A_run_abroad_counts_by_the_local_time_where_it_was_run()
    {
        // Sunday 23:30 in New York is already Monday 03:30 in UTC.
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Central Park", new DateTime(2026, 8, 2, 23, 30, 0), 8000, 2900, UtcOffsetHours: -4));

        var overview = await browser.GetOverviewAsync();

        DistanceInWeek(overview, new DateOnly(2026, 7, 27)).ShouldBe(8000);
        DistanceInWeek(overview, new DateOnly(2026, 8, 3)).ShouldBe(0);
    }

    [Test]
    public async Task Weeks_without_runs_show_as_zero_up_to_the_current_week()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Week one", new DateTime(2026, 9, 8, 18, 0, 0), 6000, 2400),
            new TestActivity(2, "Week three", new DateTime(2026, 9, 23, 18, 0, 0), 7000, 2800));

        var overview = await browser.GetOverviewAsync();

        overview.Weeks.Select(w => w.Start).ShouldBe(
            [new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 5)]);
        overview.Weeks.Select(w => w.Distance).ShouldBe([6000, 0, 7000, 0, 0]);
    }

    [Test]
    public async Task Without_runs_the_overview_is_empty()
    {
        var browser = _app.CreateBrowser();

        var overview = await browser.GetOverviewAsync();

        overview.Totals.ShouldBe(new TotalsResponse(0, 0, 0, 0));
        overview.Years.ShouldBeEmpty();
        overview.Weeks.ShouldBeEmpty();
        overview.Streak.ShouldBeNull();
    }

    [Test]
    public async Task The_streak_counts_the_current_week_once_it_has_a_run()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateTime(2026, 9, 24)), Run(2, new DateTime(2026, 9, 29)), Run(3, new DateTime(2026, 10, 6)));

        var streak = (await browser.GetOverviewAsync()).Streak!;

        streak.Current.ShouldBe(3);
        streak.Start.ShouldBe(new DateOnly(2026, 9, 21));
        streak.ThisWeekHasRun.ShouldBeTrue();
    }

    [Test]
    public async Task The_current_week_does_not_break_the_streak_before_its_first_run_but_a_past_week_without_one_does()
    {
        // A gap in the week of 14 Sep, then Runs on two Sundays.
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateTime(2026, 9, 8)), Run(2, new DateTime(2026, 9, 27)), Run(3, new DateTime(2026, 10, 4)));

        var streak = (await browser.GetOverviewAsync()).Streak!;

        streak.Current.ShouldBe(2);
        streak.Start.ShouldBe(new DateOnly(2026, 9, 21));
        streak.ThisWeekHasRun.ShouldBeFalse();
    }

    [Test]
    public async Task A_streak_of_none_has_no_start()
    {
        // Last week went without a Run.
        var browser = await _app.ConnectWithActivitiesAsync(Run(1, new DateTime(2026, 9, 22)));

        var streak = (await browser.GetOverviewAsync()).Streak!;

        streak.Current.ShouldBe(0);
        streak.Start.ShouldBeNull();
    }

    [Test]
    public async Task The_longest_streak_runs_from_the_monday_of_its_first_week_to_the_sunday_of_its_last_and_a_tie_goes_to_the_more_recent()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateTime(2026, 7, 28)), Run(2, new DateTime(2026, 8, 4)), Run(3, new DateTime(2026, 8, 11)),
            Run(4, new DateTime(2026, 8, 25)), Run(5, new DateTime(2026, 9, 1)), Run(6, new DateTime(2026, 9, 8)),
            Run(7, new DateTime(2026, 9, 29)));

        var streak = (await browser.GetOverviewAsync()).Streak!;

        streak.Current.ShouldBe(1);
        streak.Longest.ShouldBe(new LongestStreakResponse(3, new DateOnly(2026, 8, 24), new DateOnly(2026, 9, 13)));
    }

    [Test]
    public async Task The_ongoing_streak_is_the_longest_once_it_ties_a_past_one()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateTime(2026, 9, 1)), Run(2, new DateTime(2026, 9, 8)),
            Run(3, new DateTime(2026, 9, 22)), Run(4, new DateTime(2026, 9, 29)));

        var streak = (await browser.GetOverviewAsync()).Streak!;

        streak.Longest.ShouldBe(new LongestStreakResponse(2, new DateOnly(2026, 9, 21), new DateOnly(2026, 10, 4)));
    }

    [Test]
    public async Task The_recent_weeks_are_the_last_twelve_up_to_the_current_one()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateTime(2026, 6, 2)), Run(2, new DateTime(2026, 7, 21)), Run(3, new DateTime(2026, 8, 4)),
            Run(4, new DateTime(2026, 9, 30)), Run(5, new DateTime(2026, 10, 6)));

        var recent = (await browser.GetOverviewAsync()).Streak!.RecentWeeks;

        recent.Select(w => w.Start).ShouldBe(TrainingWeeks(new DateOnly(2026, 7, 20), 12));
        recent.Select(w => w.HasRun).ShouldBe([true, false, true, false, false, false, false, false, false, false, true, true]);
    }

    [Test]
    public async Task The_recent_weeks_start_with_the_first_run()
    {
        var browser = await _app.ConnectWithActivitiesAsync(Run(1, new DateTime(2026, 9, 22)));

        var recent = (await browser.GetOverviewAsync()).Streak!.RecentWeeks;

        recent.ShouldBe([
            new StreakWeekResponse(new DateOnly(2026, 9, 21), true),
            new StreakWeekResponse(new DateOnly(2026, 9, 28), false),
            new StreakWeekResponse(new DateOnly(2026, 10, 5), false),
        ]);
    }

    [Test]
    public async Task A_run_without_distance_keeps_the_streak_up()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateTime(2026, 9, 22)),
            new TestActivity(2, "Treadmill without distance", new DateTime(2026, 9, 30, 18, 0, 0), 0, 1800, SportType: "VirtualRun"));

        var streak = (await browser.GetOverviewAsync()).Streak!;

        streak.Current.ShouldBe(2);
    }

    [Test]
    public async Task A_run_dated_after_this_week_does_not_lengthen_the_longest_streak()
    {
        // Monday morning abroad, while it is still Sunday here: a week ahead of this one.
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateTime(2026, 9, 29)), Run(2, new DateTime(2026, 10, 6)), Run(3, new DateTime(2026, 10, 12)));

        var streak = (await browser.GetOverviewAsync()).Streak!;

        streak.Current.ShouldBe(2);
        streak.Longest.ShouldBe(new LongestStreakResponse(2, new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 11)));
    }

    [Test]
    public async Task Training_time_adds_each_weeks_rides_and_swims_next_to_its_runs_without_changing_the_running_figures()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Tempo run", new DateTime(2026, 9, 29, 18, 0, 0), 10000, 3000),
            new TestActivity(2, "Commute", new DateTime(2026, 9, 30, 7, 30, 0), 6000, 1500, "Ride"),
            new TestActivity(3, "Zwift Watopia", new DateTime(2026, 10, 1, 19, 0, 0), 30000, 3600, "VirtualRide"),
            new TestActivity(4, "Pool", new DateTime(2026, 10, 2, 7, 0, 0), 2000, 2400, "Swim"),
            new TestActivity(5, "Easy run", new DateTime(2026, 10, 6, 18, 0, 0), 6000, 2100));

        var overview = await browser.GetOverviewAsync();

        overview.TrainingTime.ShouldBe([
            new TrainingTimeWeekResponse(new DateOnly(2026, 9, 28), 3000, 5100, 2400),
            new TrainingTimeWeekResponse(new DateOnly(2026, 10, 5), 2100, 0, 0),
        ]);
        overview.Totals.ShouldBe(new TotalsResponse(2, 16000, 5100, 0));
        overview.Weeks.Select(w => w.Distance).ShouldBe([10000, 6000]);
    }

    [Test]
    public async Task Training_time_starts_with_a_ride_before_the_first_run_and_shows_empty_weeks_as_zero()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Gravel Marchfeld", new DateTime(2026, 9, 13, 9, 0, 0), 61200, 9000, "GravelRide"),
            new TestActivity(2, "First run", new DateTime(2026, 9, 29, 18, 0, 0), 5000, 1800));

        var overview = await browser.GetOverviewAsync();

        overview.TrainingTime.Select(w => w.Start).ShouldBe(TrainingWeeks(new DateOnly(2026, 9, 7), 5));
        overview.TrainingTime.Select(w => w.Rides).ShouldBe([9000, 0, 0, 0, 0]);
        overview.TrainingTime.Select(w => w.Runs).ShouldBe([0, 0, 0, 1800, 0]);
        // The running weeks still start with the first Run.
        overview.Weeks[0].Start.ShouldBe(new DateOnly(2026, 9, 28));
    }

    [Test]
    public async Task Without_rides_or_swims_there_is_no_training_time()
    {
        var browser = await _app.ConnectWithActivitiesAsync(Run(1, new DateTime(2026, 9, 29)));

        (await browser.GetOverviewAsync()).TrainingTime.ShouldBeEmpty();
    }

    [Test]
    public async Task On_this_day_lists_the_runs_on_todays_date_a_year_ago_by_local_time_newest_first()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Two years ago", new DateTime(2024, 10, 9, 7, 0, 0), 8000, 2900),
            new TestActivity(2, "Last year's morning", new DateTime(2025, 10, 9, 7, 0, 0), 6000, 2200),
            // 10 Oct in UTC, but still 9 Oct in New York.
            new TestActivity(3, "Last year abroad", new DateTime(2025, 10, 9, 22, 30, 0), 10000, 3500, UtcOffsetHours: -4),
            new TestActivity(4, "The day before", new DateTime(2025, 10, 8, 18, 0, 0), 5000, 1800),
            new TestActivity(5, "The day after", new DateTime(2025, 10, 10, 0, 30, 0), 5000, 1800),
            new TestActivity(6, "A ride last year", new DateTime(2025, 10, 9, 9, 0, 0), 40000, 5400, "Ride"),
            new TestActivity(7, "Today", new DateTime(2026, 10, 9, 7, 0, 0), 6000, 2100));

        var overview = await browser.GetOverviewAsync();

        var onThisDay = overview.OnThisDay!;
        onThisDay.Day.ShouldBe(new DateOnly(2025, 10, 9));
        onThisDay.Runs.ShouldBe(
            [
                new OnThisDayRunResponse(3, "Last year abroad", new DateTime(2025, 10, 9, 22, 30, 0), 10000, 3500),
                new OnThisDayRunResponse(2, "Last year's morning", new DateTime(2025, 10, 9, 7, 0, 0), 6000, 2200),
            ]);
    }

    [Test]
    public async Task On_29_february_on_this_day_lists_the_runs_on_28_february_a_year_ago()
    {
        var browser = await _app.ConnectWithActivitiesAsync(new TestActivity(1, "Last February", new DateTime(2027, 2, 28, 7, 0, 0), 8000, 2900));
        _app.Clock.SetUtcNow(new DateTimeOffset(2028, 2, 29, 18, 0, 0, TimeSpan.Zero));

        var overview = await browser.GetOverviewAsync();

        overview.OnThisDay!.Runs.Select(r => r.Name).ShouldBe(["Last February"]);
    }

    private static IEnumerable<DateOnly> TrainingWeeks(DateOnly firstMonday, int count) =>
        Enumerable.Range(0, count).Select(i => firstMonday.AddDays(7 * i));

    private static TestActivity Run(long id, DateTime day) => new(id, $"Run {id}", day.AddHours(18), 6000, 2400);

    private static double DistanceInWeek(OverviewResponse overview, DateOnly monday) =>
        overview.Weeks.Where(w => w.Start == monday).ShouldHaveSingleItem().Distance;
}
