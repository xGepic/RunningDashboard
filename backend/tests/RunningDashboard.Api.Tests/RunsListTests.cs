using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Five Runs, synced once and shared, since these tests only read:
///
/// | Run                 | Type       | Date       | Distance  | Moving Time | Pace        |
/// |---------------------|------------|------------|-----------|-------------|-------------|
/// | Evening Run         | Run        | 2026-10-06 |  5,081.2 m|  2,096 s    | ≈ 412.5 s/km|
/// | Lauf am Nachmittag  | Run        | 2026-10-03 |  4,513.7 m|  1,885 s    | ≈ 417.6 s/km|
/// | Finisher.           | Run        | 2026-09-27 | 42,582.8 m| 18,043 s    | ≈ 423.7 s/km|
/// | Treadmill intervals | VirtualRun | 2026-09-24 |  6,000.0 m|  2,280 s    | = 380.0 s/km|
/// | Wienerwald trail    | TrailRun   | 2026-09-21 | 12,105.4 m|  5,940 s    | ≈ 490.7 s/km|
/// </summary>
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public sealed class RunsListTests
{
    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync() => _browser = await _app.ConnectWithActivitiesAsync(
        new TestActivity(20479305419, "Evening Run", new DateTime(2026, 10, 6, 20, 35, 26), 5081.2, 2096),
        new TestActivity(20436100848, "Lauf am Nachmittag", new DateTime(2026, 10, 3, 17, 43, 43), 4513.7, 1885),
        new TestActivity(20351282630, "Finisher.", new DateTime(2026, 9, 27, 9, 47, 0), 42582.8, 18043),
        new TestActivity(20312345678, "Treadmill intervals", new DateTime(2026, 9, 24, 18, 5, 0), 6000, 2280, "VirtualRun"),
        new TestActivity(20287654321, "Wienerwald trail", new DateTime(2026, 9, 21, 8, 30, 12), 12105.4, 5940, "TrailRun"));

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Without_options_the_newest_runs_come_first()
    {
        var page = await _browser.ListRunsAsync();

        page.Names.ShouldBe(["Evening Run", "Lauf am Nachmittag", "Finisher.", "Treadmill intervals", "Wienerwald trail"]);
        page.TotalCount.ShouldBe(5);
    }

    [TestCase("lauf", new[] { "Lauf am Nachmittag" })]
    [TestCase("RUN", new[] { "Evening Run" })]
    [TestCase("marathon", new string[0])]
    public async Task Searching_matches_part_of_the_name_ignoring_case(string search, string[] expected)
    {
        (await _browser.ListRunsAsync($"search={search}")).Names.ShouldBe(expected);
    }

    [Test]
    public async Task A_date_range_includes_both_days()
    {
        var page = await _browser.ListRunsAsync("from=2026-09-24&to=2026-10-03");

        page.Names.ShouldBe(["Lauf am Nachmittag", "Finisher.", "Treadmill intervals"]);
    }

    [TestCase("from=2026-10-03", new[] { "Evening Run", "Lauf am Nachmittag" })]
    [TestCase("to=2026-09-24", new[] { "Treadmill intervals", "Wienerwald trail" })]
    public async Task A_date_range_can_be_open_at_either_end(string query, string[] expected)
    {
        (await _browser.ListRunsAsync(query)).Names.ShouldBe(expected);
    }

    [Test]
    public async Task Road_trail_and_treadmill_runs_are_all_listed_whatever_type_is_asked_for()
    {
        (await _browser.ListRunsAsync("sportType=TrailRun")).Names.ShouldBe(
            ["Evening Run", "Lauf am Nachmittag", "Finisher.", "Treadmill intervals", "Wienerwald trail"]);
    }

    [Test]
    public async Task Search_and_date_range_combine()
    {
        var page = await _browser.ListRunsAsync("search=e&from=2026-09-25");

        page.Names.ShouldBe(["Evening Run", "Finisher."]);
        page.TotalCount.ShouldBe(2);
    }

    [TestCase("date", "asc", new[] { "Wienerwald trail", "Treadmill intervals", "Finisher.", "Lauf am Nachmittag", "Evening Run" })]
    [TestCase("distance", "desc", new[] { "Finisher.", "Wienerwald trail", "Treadmill intervals", "Evening Run", "Lauf am Nachmittag" })]
    [TestCase("distance", "asc", new[] { "Lauf am Nachmittag", "Evening Run", "Treadmill intervals", "Wienerwald trail", "Finisher." })]
    [TestCase("movingTime", "asc", new[] { "Lauf am Nachmittag", "Evening Run", "Treadmill intervals", "Wienerwald trail", "Finisher." })]
    [TestCase("movingTime", "desc", new[] { "Finisher.", "Wienerwald trail", "Treadmill intervals", "Evening Run", "Lauf am Nachmittag" })]
    [TestCase("pace", "asc", new[] { "Treadmill intervals", "Evening Run", "Lauf am Nachmittag", "Finisher.", "Wienerwald trail" })]
    [TestCase("pace", "desc", new[] { "Wienerwald trail", "Finisher.", "Lauf am Nachmittag", "Evening Run", "Treadmill intervals" })]
    public async Task Runs_sort_by_each_key_in_both_directions(string sort, string direction, string[] expected)
    {
        (await _browser.ListRunsAsync($"sort={sort}&direction={direction}")).Names.ShouldBe(expected);
    }

    [TestCase(1, new[] { "Evening Run", "Lauf am Nachmittag" })]
    [TestCase(3, new[] { "Wienerwald trail" })]
    [TestCase(4, new string[0])]
    public async Task Pages_split_the_list_and_report_the_total(int pageNumber, string[] expected)
    {
        var page = await _browser.ListRunsAsync($"page={pageNumber}&pageSize=2");

        page.Names.ShouldBe(expected);
        page.TotalCount.ShouldBe(5);
        page.Page.ShouldBe(pageNumber);
        page.PageSize.ShouldBe(2);
    }

    [TestCase("page=0")]
    [TestCase("pageSize=0")]
    [TestCase("pageSize=201")]
    [TestCase("sort=name")]
    [TestCase("direction=up")]
    [TestCase("from=2026-10-03&to=2026-09-24")]
    public async Task Invalid_options_are_refused(string query)
    {
        var response = await _browser.GetAsync($"/api/runs?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Months_add_up_a_years_runs_per_calendar_month()
    {
        var year = await MonthsAsync("year=2026");

        (year.Year, year.FirstYear).ShouldBe((2026, 2026));
        year.Months.Select(m => m.Month).ShouldBe(Enumerable.Range(1, 12));
        var september = year.Months[8];
        (september.Runs, september.MovingTime).ShouldBe((3, 26263));
        september.Distance.ShouldBe(60688.2, 0.05);
        // 26,263 s over 60.6882 km.
        september.Pace!.Value.ShouldBe(432.75, 0.005);
        var october = year.Months[9];
        (october.Runs, october.MovingTime).ShouldBe((2, 3981));
        october.Distance.ShouldBe(9594.9, 0.05);
        year.Months[0].ShouldBe(new MonthTotals(1, 0, 0, 0, null));
    }

    [Test]
    public async Task The_years_runs_are_added_up_per_day_of_the_week_too_monday_first()
    {
        var year = await MonthsAsync("year=2026");

        year.Weekdays.Select(d => (d.Day, d.Runs)).ShouldBe([
            ("monday", 1), ("tuesday", 1), ("wednesday", 0), ("thursday", 1), ("friday", 0), ("saturday", 1), ("sunday", 1),
        ]);
        year.Weekdays[6].Distance.ShouldBe(42582.8, 0.05);
        year.Weekdays[2].Distance.ShouldBe(0);
    }

    [Test]
    public async Task A_run_counts_for_the_day_of_the_week_of_its_local_start()
    {
        await using var app = new TestApp();
        var browser = await app.ConnectWithActivitiesAsync(
            new TestActivity(20500000011, "Morning run", new DateTime(2026, 3, 4, 6, 30, 0), 8000, 2900),
            new TestActivity(20500000012, "Late shakeout", new DateTime(2026, 3, 4, 23, 30, 0), 3000, 1100),
            new TestActivity(20500000013, "Just after midnight", new DateTime(2026, 3, 5, 0, 10, 0), 5000, 1800),
            new TestActivity(20500000014, "A week later", new DateTime(2026, 3, 11, 7, 0, 0), 6000, 2200));
        await TestApp.WaitForSyncAsync(browser);

        var year = (await browser.GetFromJsonAsync<RunMonths>("/api/runs/months?year=2026"))!;

        (year.Weekdays[2], year.Weekdays[3]).ShouldBe((new WeekdayTotals("wednesday", 3, 17000), new WeekdayTotals("thursday", 1, 5000)));
    }

    [Test]
    public async Task Months_are_for_the_current_year_by_default()
    {
        (await MonthsAsync("")).Year.ShouldBe(2026);
    }

    [Test]
    public async Task Months_count_road_trail_and_treadmill_runs_alike_whatever_type_is_asked_for()
    {
        var year = await MonthsAsync("year=2026&sportType=TrailRun");

        (year.Months[8].Runs, year.Months[9].Runs).ShouldBe((3, 2));
    }

    [Test]
    public async Task A_year_without_runs_has_twelve_empty_months_and_still_names_the_first_year()
    {
        var year = await MonthsAsync("year=2025");

        year.Months.ShouldAllBe(m => m.Runs == 0 && m.Distance == 0);
        year.FirstYear.ShouldBe(2026);
    }

    [Test]
    public async Task A_run_counts_for_the_year_of_its_local_start_right_up_to_new_years_eve_midnight()
    {
        await using var app = new TestApp();
        var browser = await app.ConnectWithActivitiesAsync(
            new TestActivity(20500000001, "New Year's Day run", new DateTime(2026, 1, 1, 0, 0, 0), 5000, 1800),
            new TestActivity(20500000002, "Silvesterlauf", new DateTime(2025, 12, 31, 23, 59, 59), 10000, 3000),
            new TestActivity(20500000003, "Summer jog", new DateTime(2024, 6, 1, 7, 0, 0), 8000, 2900));
        await TestApp.WaitForSyncAsync(browser);
        async Task<RunMonths> months(int year) => (await browser.GetFromJsonAsync<RunMonths>($"/api/runs/months?year={year}"))!;

        var year2025 = await months(2025);
        var year2026 = await months(2026);

        (year2025.Months[11].Runs, year2025.Months.Sum(m => m.Runs), year2025.FirstYear).ShouldBe((1, 1, 2024));
        (year2026.Months[0].Runs, year2026.Months.Sum(m => m.Runs)).ShouldBe((1, 1));
    }

    [TestCase("year=1899")]
    public async Task Invalid_month_options_are_refused(string query)
    {
        var response = await _browser.GetAsync($"/api/runs/months?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<RunMonths> MonthsAsync(string query) =>
        (await _browser.GetFromJsonAsync<RunMonths>($"/api/runs/months?{query}"))!;

    private sealed record RunMonths(int Year, int? FirstYear, List<MonthTotals> Months, List<WeekdayTotals> Weekdays);

    private sealed record WeekdayTotals(string Day, int Runs, double Distance);

    private sealed record MonthTotals(int Month, int Runs, double Distance, int MovingTime, double? Pace);
}
