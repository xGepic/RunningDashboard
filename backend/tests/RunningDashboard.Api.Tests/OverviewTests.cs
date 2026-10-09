using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>The clock stands at Friday 2026-10-09, so the current Training Week starts Monday 2026-10-05.</summary>
public sealed class OverviewTests : IAsyncLifetime
{
    private readonly TestApp _app = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Totals_add_up_all_runs_and_each_calendar_year_by_local_time()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "New Year's Eve run", new DateTime(2025, 12, 31, 15, 0, 0), 5000, 1800, UtcOffsetHours: 1, ElevationGain: 20),
            // 2025-12-31T23:30Z in UTC, but 2026 where it was run.
            new TestActivity(2, "New Year's run", new DateTime(2026, 1, 1, 0, 30, 0), 10000, 3600, UtcOffsetHours: 1, ElevationGain: 50),
            new TestActivity(3, "Spring run", new DateTime(2026, 3, 10, 18, 0, 0), 8000, 3000, UtcOffsetHours: 1, ElevationGain: 30));

        var overview = await GetOverviewAsync(browser);

        Assert.Equal(new TotalsResponse(3, 23000, 8400, 100), overview.Totals);
        Assert.Equal(
            [new YearResponse(2025, 1, 5000, 1800, 20), new YearResponse(2026, 2, 18000, 6600, 80)],
            overview.Years);
    }

    [Fact]
    public async Task A_run_late_on_sunday_counts_in_the_week_that_started_on_monday()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Sunday night", new DateTime(2026, 9, 27, 23, 30, 0), 10000, 3600),
            new TestActivity(2, "Monday just after midnight", new DateTime(2026, 9, 28, 0, 10, 0), 5000, 1800));

        var overview = await GetOverviewAsync(browser);

        Assert.Equal(10000, DistanceInWeek(overview, new DateOnly(2026, 9, 21)));
        Assert.Equal(5000, DistanceInWeek(overview, new DateOnly(2026, 9, 28)));
    }

    [Fact]
    public async Task A_run_abroad_counts_by_the_local_time_where_it_was_run()
    {
        // Sunday 23:30 in New York is already Monday 03:30 in UTC.
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Central Park", new DateTime(2026, 8, 2, 23, 30, 0), 8000, 2900, UtcOffsetHours: -4));

        var overview = await GetOverviewAsync(browser);

        Assert.Equal(8000, DistanceInWeek(overview, new DateOnly(2026, 7, 27)));
        Assert.Equal(0, DistanceInWeek(overview, new DateOnly(2026, 8, 3)));
    }

    [Fact]
    public async Task Monthly_pace_is_total_moving_time_over_total_distance()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Fast long run", new DateTime(2026, 9, 6, 9, 0, 0), 10000, 3000),
            new TestActivity(2, "Slow short run", new DateTime(2026, 9, 8, 18, 0, 0), 2000, 1000),
            new TestActivity(3, "July run", new DateTime(2026, 7, 15, 18, 0, 0), 5000, 2000));

        var overview = await GetOverviewAsync(browser);

        // 4000 s over 12 km, not the average of 300 and 500 s/km.
        var september = Assert.Single(overview.Months, m => m is { Year: 2026, Month: 9 });
        Assert.Equal(333.3, september.Pace!.Value, 1);
        Assert.Equal(12000, september.Distance);
        var august = Assert.Single(overview.Months, m => m is { Year: 2026, Month: 8 });
        Assert.Null(august.Pace);
    }

    [Fact]
    public async Task Weeks_without_runs_show_as_zero_up_to_the_current_week()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Week one", new DateTime(2026, 9, 8, 18, 0, 0), 6000, 2400),
            new TestActivity(2, "Week three", new DateTime(2026, 9, 23, 18, 0, 0), 7000, 2800));

        var overview = await GetOverviewAsync(browser);

        Assert.Equal(
            [new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 5)],
            overview.Weeks.Select(w => w.Start));
        Assert.Equal([6000, 0, 7000, 0, 0], overview.Weeks.Select(w => w.Distance));
    }

    [Fact]
    public async Task Without_runs_the_overview_is_empty()
    {
        var browser = _app.CreateBrowser();

        var overview = await GetOverviewAsync(browser);

        Assert.Equal(new TotalsResponse(0, 0, 0, 0), overview.Totals);
        Assert.Empty(overview.Years);
        Assert.Empty(overview.Weeks);
        Assert.Empty(overview.Months);
    }

    private static double DistanceInWeek(OverviewResponse overview, DateOnly monday) =>
        Assert.Single(overview.Weeks, w => w.Start == monday).Distance;

    private static async Task<OverviewResponse> GetOverviewAsync(HttpClient browser) =>
        (await browser.GetFromJsonAsync<OverviewResponse>("/api/overview"))!;

    private sealed record OverviewResponse(TotalsResponse Totals, List<YearResponse> Years, List<WeekResponse> Weeks, List<MonthResponse> Months);

    private sealed record TotalsResponse(int Runs, double Distance, int MovingTime, double ElevationGain);

    private sealed record YearResponse(int Year, int Runs, double Distance, int MovingTime, double ElevationGain);

    private sealed record WeekResponse(DateOnly Start, double Distance);

    private sealed record MonthResponse(int Year, int Month, double Distance, double? Pace);
}
