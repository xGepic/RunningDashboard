using System.Net.Http.Json;
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

        var overview = await GetOverviewAsync(browser);

        overview.Totals.ShouldBe(new TotalsResponse(3, 23000, 8400, 100));
        overview.Years.ShouldBe(
            [new YearResponse(2025, 1, 5000, 1800, 20), new YearResponse(2026, 2, 18000, 6600, 80)]);
    }

    [Test]
    public async Task A_run_late_on_sunday_counts_in_the_week_that_started_on_monday()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Sunday night", new DateTime(2026, 9, 27, 23, 30, 0), 10000, 3600),
            new TestActivity(2, "Monday just after midnight", new DateTime(2026, 9, 28, 0, 10, 0), 5000, 1800));

        var overview = await GetOverviewAsync(browser);

        DistanceInWeek(overview, new DateOnly(2026, 9, 21)).ShouldBe(10000);
        DistanceInWeek(overview, new DateOnly(2026, 9, 28)).ShouldBe(5000);
    }

    [Test]
    public async Task A_run_abroad_counts_by_the_local_time_where_it_was_run()
    {
        // Sunday 23:30 in New York is already Monday 03:30 in UTC.
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Central Park", new DateTime(2026, 8, 2, 23, 30, 0), 8000, 2900, UtcOffsetHours: -4));

        var overview = await GetOverviewAsync(browser);

        DistanceInWeek(overview, new DateOnly(2026, 7, 27)).ShouldBe(8000);
        DistanceInWeek(overview, new DateOnly(2026, 8, 3)).ShouldBe(0);
    }

    [Test]
    public async Task Monthly_pace_is_total_moving_time_over_total_distance()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Fast long run", new DateTime(2026, 9, 6, 9, 0, 0), 10000, 3000),
            new TestActivity(2, "Slow short run", new DateTime(2026, 9, 8, 18, 0, 0), 2000, 1000),
            new TestActivity(3, "July run", new DateTime(2026, 7, 15, 18, 0, 0), 5000, 2000));

        var overview = await GetOverviewAsync(browser);

        // 4000 s over 12 km, not the average of 300 and 500 s/km.
        var september = overview.Months.Where(m => m is { Year: 2026, Month: 9 }).ShouldHaveSingleItem();
        september.Pace!.Value.ShouldBe(333.3, 0.05);
        september.Distance.ShouldBe(12000);
        var august = overview.Months.Where(m => m is { Year: 2026, Month: 8 }).ShouldHaveSingleItem();
        august.Pace.ShouldBeNull();
    }

    [Test]
    public async Task Weeks_without_runs_show_as_zero_up_to_the_current_week()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Week one", new DateTime(2026, 9, 8, 18, 0, 0), 6000, 2400),
            new TestActivity(2, "Week three", new DateTime(2026, 9, 23, 18, 0, 0), 7000, 2800));

        var overview = await GetOverviewAsync(browser);

        overview.Weeks.Select(w => w.Start).ShouldBe(
            [new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 5)]);
        overview.Weeks.Select(w => w.Distance).ShouldBe([6000, 0, 7000, 0, 0]);
    }

    [Test]
    public async Task Each_day_with_runs_has_its_total_distance_and_a_double_is_added_up()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Morning", new DateTime(2026, 9, 15, 7, 0, 0), 8000, 2900),
            new TestActivity(2, "Evening", new DateTime(2026, 9, 15, 19, 0, 0), 5000, 1800),
            new TestActivity(3, "Long run", new DateTime(2026, 9, 20, 9, 0, 0), 20000, 7200));

        var overview = await GetOverviewAsync(browser);

        // Days without Runs are left out.
        overview.Days.ShouldBe([new DayResponse(new DateOnly(2026, 9, 15), 13000), new DayResponse(new DateOnly(2026, 9, 20), 20000)]);
    }

    [Test]
    public async Task A_run_after_midnight_or_abroad_counts_on_the_date_where_it_was_run()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            // Still Sunday 22:10 in UTC.
            new TestActivity(1, "Monday just after midnight", new DateTime(2026, 9, 28, 0, 10, 0), 5000, 1800),
            // Already Monday 03:30 in UTC.
            new TestActivity(2, "Central Park", new DateTime(2026, 8, 2, 23, 30, 0), 8000, 2900, UtcOffsetHours: -4));

        var overview = await GetOverviewAsync(browser);

        overview.Days.ShouldBe([new DayResponse(new DateOnly(2026, 8, 2), 8000), new DayResponse(new DateOnly(2026, 9, 28), 5000)]);
    }

    [Test]
    public async Task Days_keep_their_own_year_where_a_training_week_spans_new_year()
    {
        // The Training Week of Mon 29 Dec 2025 to Sun 4 Jan 2026.
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "New Year's Eve run", new DateTime(2025, 12, 31, 15, 0, 0), 5000, 1800, UtcOffsetHours: 1),
            new TestActivity(2, "New Year's run", new DateTime(2026, 1, 1, 0, 30, 0), 10000, 3600, UtcOffsetHours: 1),
            new TestActivity(3, "First Sunday", new DateTime(2026, 1, 4, 10, 0, 0), 12000, 4300, UtcOffsetHours: 1));

        var overview = await GetOverviewAsync(browser);

        overview.Days.ShouldBe(
            [new DayResponse(new DateOnly(2025, 12, 31), 5000), new DayResponse(new DateOnly(2026, 1, 1), 10000), new DayResponse(new DateOnly(2026, 1, 4), 12000)]);
        DistanceInWeek(overview, new DateOnly(2025, 12, 29)).ShouldBe(27000);
    }

    [Test]
    public async Task Without_runs_the_overview_is_empty()
    {
        var browser = _app.CreateBrowser();

        var overview = await GetOverviewAsync(browser);

        overview.Totals.ShouldBe(new TotalsResponse(0, 0, 0, 0));
        overview.Years.ShouldBeEmpty();
        overview.Weeks.ShouldBeEmpty();
        overview.Months.ShouldBeEmpty();
        overview.Days.ShouldBeEmpty();
    }

    private static double DistanceInWeek(OverviewResponse overview, DateOnly monday) =>
        overview.Weeks.Where(w => w.Start == monday).ShouldHaveSingleItem().Distance;

    private static async Task<OverviewResponse> GetOverviewAsync(HttpClient browser) =>
        (await browser.GetFromJsonAsync<OverviewResponse>("/api/overview"))!;

    /// <param name="Days">Each local date with Runs, oldest first.</param>
    private sealed record OverviewResponse(TotalsResponse Totals, List<YearResponse> Years, List<WeekResponse> Weeks, List<MonthResponse> Months, List<DayResponse> Days);

    private sealed record TotalsResponse(int Runs, double Distance, int MovingTime, double ElevationGain);

    private sealed record YearResponse(int Year, int Runs, double Distance, int MovingTime, double ElevationGain);

    private sealed record WeekResponse(DateOnly Start, double Distance);

    private sealed record MonthResponse(int Year, int Month, double Distance, double? Pace);

    /// <param name="Distance">Metres run that day, all Runs together.</param>
    private sealed record DayResponse(DateOnly Date, double Distance);
}
