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
        var page = await ListAsync("");

        page.Names.ShouldBe(["Evening Run", "Lauf am Nachmittag", "Finisher.", "Treadmill intervals", "Wienerwald trail"]);
        page.TotalCount.ShouldBe(5);
    }

    [TestCase("lauf", new[] { "Lauf am Nachmittag" })]
    [TestCase("RUN", new[] { "Evening Run" })]
    [TestCase("marathon", new string[0])]
    public async Task Searching_matches_part_of_the_name_ignoring_case(string search, string[] expected)
    {
        (await ListAsync($"search={search}")).Names.ShouldBe(expected);
    }

    [Test]
    public async Task A_date_range_includes_both_days()
    {
        var page = await ListAsync("from=2026-09-24&to=2026-10-03");

        page.Names.ShouldBe(["Lauf am Nachmittag", "Finisher.", "Treadmill intervals"]);
    }

    [TestCase("from=2026-10-03", new[] { "Evening Run", "Lauf am Nachmittag" })]
    [TestCase("to=2026-09-24", new[] { "Treadmill intervals", "Wienerwald trail" })]
    public async Task A_date_range_can_be_open_at_either_end(string query, string[] expected)
    {
        (await ListAsync(query)).Names.ShouldBe(expected);
    }

    [Test]
    public async Task Road_trail_and_treadmill_runs_are_all_listed_whatever_type_is_asked_for()
    {
        (await ListAsync("sportType=TrailRun")).Names.ShouldBe(
            ["Evening Run", "Lauf am Nachmittag", "Finisher.", "Treadmill intervals", "Wienerwald trail"]);
    }

    [Test]
    public async Task Search_and_date_range_combine()
    {
        var page = await ListAsync("search=e&from=2026-09-25");

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
        (await ListAsync($"sort={sort}&direction={direction}")).Names.ShouldBe(expected);
    }

    [TestCase(1, new[] { "Evening Run", "Lauf am Nachmittag" })]
    [TestCase(3, new[] { "Wienerwald trail" })]
    [TestCase(4, new string[0])]
    public async Task Pages_split_the_list_and_report_the_total(int pageNumber, string[] expected)
    {
        var page = await ListAsync($"page={pageNumber}&pageSize=2");

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

    [TestCase("year=1899")]
    public async Task Invalid_month_options_are_refused(string query)
    {
        var response = await _browser.GetAsync($"/api/runs/months?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<RunMonths> MonthsAsync(string query) =>
        (await _browser.GetFromJsonAsync<RunMonths>($"/api/runs/months?{query}"))!;

    private sealed record RunMonths(int Year, int? FirstYear, List<MonthTotals> Months);

    private sealed record MonthTotals(int Month, int Runs, double Distance, int MovingTime, double? Pace);

    private async Task<RunPage> ListAsync(string query) =>
        (await _browser.GetFromJsonAsync<RunPage>($"/api/runs?{query}"))!;

    private sealed record RunPage(List<RunItem> Items, int Page, int PageSize, int TotalCount)
    {
        public List<string> Names => [.. Items.Select(i => i.Name)];
    }

    private sealed record RunItem(long Id, string Name);
}
