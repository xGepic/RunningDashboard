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
public sealed class RunsListTests(RunsListTests.FiveRuns runs) : IClassFixture<RunsListTests.FiveRuns>
{
    private readonly HttpClient _browser = runs.Browser;

    [Fact]
    public async Task Without_options_the_newest_runs_come_first()
    {
        var page = await ListAsync("");

        Assert.Equal(["Evening Run", "Lauf am Nachmittag", "Finisher.", "Treadmill intervals", "Wienerwald trail"], page.Names);
        Assert.Equal(5, page.TotalCount);
    }

    [Theory]
    [InlineData("lauf", new[] { "Lauf am Nachmittag" })]
    [InlineData("RUN", new[] { "Evening Run" })]
    [InlineData("marathon", new string[0])]
    public async Task Searching_matches_part_of_the_name_ignoring_case(string search, string[] expected)
    {
        Assert.Equal(expected, (await ListAsync($"search={search}")).Names);
    }

    [Fact]
    public async Task A_date_range_includes_both_days()
    {
        var page = await ListAsync("from=2026-09-24&to=2026-10-03");

        Assert.Equal(["Lauf am Nachmittag", "Finisher.", "Treadmill intervals"], page.Names);
    }

    [Theory]
    [InlineData("from=2026-10-03", new[] { "Evening Run", "Lauf am Nachmittag" })]
    [InlineData("to=2026-09-24", new[] { "Treadmill intervals", "Wienerwald trail" })]
    public async Task A_date_range_can_be_open_at_either_end(string query, string[] expected)
    {
        Assert.Equal(expected, (await ListAsync(query)).Names);
    }

    [Theory]
    [InlineData("TrailRun", new[] { "Wienerwald trail" })]
    [InlineData("VirtualRun", new[] { "Treadmill intervals" })]
    [InlineData("Run", new[] { "Evening Run", "Lauf am Nachmittag", "Finisher." })]
    public async Task Filtering_by_sport_type_keeps_only_that_type(string sportType, string[] expected)
    {
        Assert.Equal(expected, (await ListAsync($"sportType={sportType}")).Names);
    }

    [Fact]
    public async Task Search_date_range_and_sport_type_combine()
    {
        var page = await ListAsync("search=e&from=2026-09-25&sportType=Run");

        Assert.Equal(["Evening Run", "Finisher."], page.Names);
        Assert.Equal(2, page.TotalCount);
    }

    [Theory]
    [InlineData("date", "asc", new[] { "Wienerwald trail", "Treadmill intervals", "Finisher.", "Lauf am Nachmittag", "Evening Run" })]
    [InlineData("distance", "desc", new[] { "Finisher.", "Wienerwald trail", "Treadmill intervals", "Evening Run", "Lauf am Nachmittag" })]
    [InlineData("distance", "asc", new[] { "Lauf am Nachmittag", "Evening Run", "Treadmill intervals", "Wienerwald trail", "Finisher." })]
    [InlineData("movingTime", "asc", new[] { "Lauf am Nachmittag", "Evening Run", "Treadmill intervals", "Wienerwald trail", "Finisher." })]
    [InlineData("movingTime", "desc", new[] { "Finisher.", "Wienerwald trail", "Treadmill intervals", "Evening Run", "Lauf am Nachmittag" })]
    [InlineData("pace", "asc", new[] { "Treadmill intervals", "Evening Run", "Lauf am Nachmittag", "Finisher.", "Wienerwald trail" })]
    [InlineData("pace", "desc", new[] { "Wienerwald trail", "Finisher.", "Lauf am Nachmittag", "Evening Run", "Treadmill intervals" })]
    public async Task Runs_sort_by_each_key_in_both_directions(string sort, string direction, string[] expected)
    {
        Assert.Equal(expected, (await ListAsync($"sort={sort}&direction={direction}")).Names);
    }

    [Theory]
    [InlineData(1, new[] { "Evening Run", "Lauf am Nachmittag" })]
    [InlineData(3, new[] { "Wienerwald trail" })]
    [InlineData(4, new string[0])]
    public async Task Pages_split_the_list_and_report_the_total(int pageNumber, string[] expected)
    {
        var page = await ListAsync($"page={pageNumber}&pageSize=2");

        Assert.Equal(expected, page.Names);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(pageNumber, page.Page);
        Assert.Equal(2, page.PageSize);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=201")]
    [InlineData("sort=name")]
    [InlineData("direction=up")]
    [InlineData("sportType=Ride")]
    [InlineData("from=2026-10-03&to=2026-09-24")]
    public async Task Invalid_options_are_refused(string query)
    {
        var response = await _browser.GetAsync($"/api/runs?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Months_add_up_a_years_runs_per_calendar_month()
    {
        var year = await MonthsAsync("year=2026");

        Assert.Equal((2026, 2026), (year.Year, year.FirstYear));
        Assert.Equal(Enumerable.Range(1, 12), year.Months.Select(m => m.Month));
        var september = year.Months[8];
        Assert.Equal((3, 26263), (september.Runs, september.MovingTime));
        Assert.Equal(60688.2, september.Distance, 1);
        // 26,263 s over 60.6882 km.
        Assert.Equal(432.75, september.Pace!.Value, 2);
        var october = year.Months[9];
        Assert.Equal((2, 3981), (october.Runs, october.MovingTime));
        Assert.Equal(9594.9, october.Distance, 1);
        Assert.Equal(new MonthTotals(1, 0, 0, 0, null), year.Months[0]);
    }

    [Fact]
    public async Task Months_are_for_the_current_year_by_default()
    {
        Assert.Equal(2026, (await MonthsAsync("")).Year);
    }

    [Fact]
    public async Task Months_count_only_the_sport_type_filtered_for()
    {
        var year = await MonthsAsync("year=2026&sportType=TrailRun");

        Assert.Equal((1, 12105.4), (year.Months[8].Runs, year.Months[8].Distance));
        Assert.Equal(0, year.Months[9].Runs);
    }

    [Fact]
    public async Task A_year_without_runs_has_twelve_empty_months_and_still_names_the_first_year()
    {
        var year = await MonthsAsync("year=2025");

        Assert.All(year.Months, m => Assert.Equal((0, 0.0), (m.Runs, m.Distance)));
        Assert.Equal(2026, year.FirstYear);
    }

    [Theory]
    [InlineData("year=1899")]
    [InlineData("sportType=Ride")]
    public async Task Invalid_month_options_are_refused(string query)
    {
        var response = await _browser.GetAsync($"/api/runs/months?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    public sealed class FiveRuns : IAsyncLifetime
    {
        private readonly TestApp _app = new();

        public HttpClient Browser { get; private set; } = null!;

        public async Task InitializeAsync() => Browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(20479305419, "Evening Run", new DateTime(2026, 10, 6, 20, 35, 26), 5081.2, 2096),
            new TestActivity(20436100848, "Lauf am Nachmittag", new DateTime(2026, 10, 3, 17, 43, 43), 4513.7, 1885),
            new TestActivity(20351282630, "Finisher.", new DateTime(2026, 9, 27, 9, 47, 0), 42582.8, 18043),
            new TestActivity(20312345678, "Treadmill intervals", new DateTime(2026, 9, 24, 18, 5, 0), 6000, 2280, "VirtualRun"),
            new TestActivity(20287654321, "Wienerwald trail", new DateTime(2026, 9, 21, 8, 30, 12), 12105.4, 5940, "TrailRun"));

        public async Task DisposeAsync() => await _app.DisposeAsync();
    }
}
