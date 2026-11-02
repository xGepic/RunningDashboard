using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Four Swims among other Activities, synced once and shared, since these tests only read:
///
/// | Swim                   | Date       | Distance | Moving Time | Swim Pace        |
/// |------------------------|------------|----------|-------------|------------------|
/// | Pool 2k                | 2026-10-05 |  2,000 m |   2,400 s   | = 120 s /100 m   |
/// | Open water Alte Donau  | 2026-10-01 |  1,500 m |   2,550 s   | = 170 s /100 m   |
/// | Technique 1k           | 2026-09-26 |  1,000 m |   1,350 s   | = 135 s /100 m   |
/// | Lake 3k                | 2026-09-20 |  3,000 m |   3,150 s   | = 105 s /100 m   |
///
/// The Pool 2k was recorded with heart rate, the Open water Alte Donau without.
///
/// Not Swims: an Evening Run, a Ride and a walk.
/// </summary>
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public sealed class SwimsListTests
{
    private const long Pool2K = 20460000011;
    private const long OpenWaterAlteDonau = 20430000013;
    private const long Ride = 20440000012;

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync() => _browser = await _app.ConnectWithActivitiesAsync(
        new TestActivity(20479305419, "Evening Run", new DateTime(2026, 10, 6, 20, 35, 26), 5081.2, 2096),
        new TestActivity(Pool2K, "Pool 2k", new DateTime(2026, 10, 5, 7, 0, 0), 2000, 2400, "Swim", ElapsedTime: 2700,
            AverageHeartRate: 132.4, MaxHeartRate: 158),
        new TestActivity(Ride, "Kahlenberg climb", new DateTime(2026, 10, 2, 11, 0, 0), 42300, 5400, "Ride"),
        new TestActivity(OpenWaterAlteDonau, "Open water Alte Donau", new DateTime(2026, 10, 1, 18, 0, 0), 1500, 2550, "Swim"),
        new TestActivity(20420000014, "Sunday walk", new DateTime(2026, 9, 27, 15, 0, 0), 4000, 3000, "Walk"),
        new TestActivity(20410000015, "Technique 1k", new DateTime(2026, 9, 26, 7, 0, 0), 1000, 1350, "Swim"),
        new TestActivity(20400000016, "Lake 3k", new DateTime(2026, 9, 20, 9, 0, 0), 3000, 3150, "Swim"));

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Swims_are_listed_newest_first_and_nothing_else()
    {
        var page = await ListAsync("");

        page.Names.ShouldBe(["Pool 2k", "Open water Alte Donau", "Technique 1k", "Lake 3k"]);
        page.TotalCount.ShouldBe(4);
    }

    [Test]
    public async Task A_swim_is_listed_with_its_swim_pace_and_link_to_strava()
    {
        var swim = (await ListAsync("")).Items[0];

        (swim.Id, swim.StartLocal).ShouldBe((Pool2K, new DateTime(2026, 10, 5, 7, 0, 0)));
        (swim.Distance, swim.MovingTime).ShouldBe((2000, 2400));
        swim.Pace!.Value.ShouldBe(120, 0.0005); // 40 minutes for 20 × 100 m
        swim.StravaUrl.ShouldBe($"https://www.strava.com/activities/{Pool2K}");
    }

    [TestCase("donau", new[] { "Open water Alte Donau" })]
    [TestCase("3K", new[] { "Lake 3k" })]
    [TestCase("Evening", new string[0])]
    public async Task Searching_matches_part_of_the_name_ignoring_case(string search, string[] expected)
    {
        (await ListAsync($"search={search}")).Names.ShouldBe(expected);
    }

    [Test]
    public async Task A_date_range_includes_both_days()
    {
        var page = await ListAsync("from=2026-09-26&to=2026-10-01");

        page.Names.ShouldBe(["Open water Alte Donau", "Technique 1k"]);
        page.TotalCount.ShouldBe(2);
    }

    [TestCase("date", "asc", new[] { "Lake 3k", "Technique 1k", "Open water Alte Donau", "Pool 2k" })]
    [TestCase("distance", "desc", new[] { "Lake 3k", "Pool 2k", "Open water Alte Donau", "Technique 1k" })]
    [TestCase("distance", "asc", new[] { "Technique 1k", "Open water Alte Donau", "Pool 2k", "Lake 3k" })]
    [TestCase("movingTime", "desc", new[] { "Lake 3k", "Open water Alte Donau", "Pool 2k", "Technique 1k" })]
    [TestCase("movingTime", "asc", new[] { "Technique 1k", "Pool 2k", "Open water Alte Donau", "Lake 3k" })]
    [TestCase("pace", "asc", new[] { "Lake 3k", "Pool 2k", "Technique 1k", "Open water Alte Donau" })]
    [TestCase("pace", "desc", new[] { "Open water Alte Donau", "Technique 1k", "Pool 2k", "Lake 3k" })]
    public async Task Swims_sort_by_each_key_in_both_directions(string sort, string direction, string[] expected)
    {
        (await ListAsync($"sort={sort}&direction={direction}")).Names.ShouldBe(expected);
    }

    [TestCase(1, new[] { "Pool 2k", "Open water Alte Donau", "Technique 1k" })]
    [TestCase(2, new[] { "Lake 3k" })]
    [TestCase(3, new string[0])]
    public async Task Pages_split_the_list_and_report_the_total(int pageNumber, string[] expected)
    {
        var page = await ListAsync($"page={pageNumber}&pageSize=3");

        page.Names.ShouldBe(expected);
        (page.TotalCount, page.Page, page.PageSize).ShouldBe((4, pageNumber, 3));
    }

    [Test]
    public async Task The_totals_cover_every_matching_swim_not_just_the_page()
    {
        var totals = (await ListAsync("pageSize=1")).Totals;

        (totals.Swims, totals.Distance, totals.MovingTime).ShouldBe((4, 7500, 9450));
        totals.Pace!.Value.ShouldBe(126, 0.0005); // 9,450 s for 75 × 100 m
    }

    [Test]
    public async Task The_years_with_a_swim_come_newest_first_whatever_is_asked_for()
    {
        await using var app = new TestApp();
        var browser = await app.ConnectWithActivitiesAsync(
            new TestActivity(20600000001, "New Year's dip", new DateTime(2026, 1, 1, 11, 0, 0), 100, 120, "Swim"),
            new TestActivity(20600000002, "Kahlenberg climb", new DateTime(2025, 6, 1, 10, 0, 0), 42300, 5400, "Ride"),
            new TestActivity(20600000003, "Silvester swim", new DateTime(2024, 12, 31, 23, 0, 0), 1000, 1350, "Swim"),
            new TestActivity(20600000004, "Summer lake", new DateTime(2024, 7, 1, 9, 0, 0), 2000, 2400, "Swim"));
        await TestApp.WaitForSyncAsync(browser);

        var page = (await browser.GetFromJsonAsync<SwimPage>("/api/swims?from=2026-01-01&to=2026-12-31&search=dip"))!;

        page.Names.ShouldBe(["New Year's dip"]);
        page.Years.ShouldBe([2026, 2024]);
    }

    [TestCase("page=0")]
    [TestCase("pageSize=0")]
    [TestCase("pageSize=201")]
    [TestCase("sort=speed")]
    [TestCase("direction=up")]
    [TestCase("from=2026-10-01&to=2026-09-26")]
    public async Task Invalid_options_are_refused(string query)
    {
        var response = await _browser.GetAsync($"/api/swims?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task A_swim_has_a_detail_page_with_its_times_heart_rate_and_link_to_strava()
    {
        var swim = (await _browser.GetFromJsonAsync<SwimDetail>($"/api/swims/{Pool2K}"))!;

        (swim.Id, swim.Name, swim.StartLocal).ShouldBe((Pool2K, "Pool 2k", new DateTime(2026, 10, 5, 7, 0, 0)));
        (swim.Distance, swim.MovingTime, swim.ElapsedTime).ShouldBe((2000, 2400, 2700));
        swim.Pace!.Value.ShouldBe(120, 0.0005);
        (swim.AverageHeartRate, swim.MaxHeartRate).ShouldBe((132.4, 158));
        swim.StravaUrl.ShouldBe($"https://www.strava.com/activities/{Pool2K}");
    }

    [Test]
    public async Task A_swim_recorded_without_heart_rate_has_none()
    {
        var swim = (await _browser.GetFromJsonAsync<SwimDetail>($"/api/swims/{OpenWaterAlteDonau}"))!;

        (swim.AverageHeartRate, swim.MaxHeartRate).ShouldBe((null, null));
    }

    [TestCase(Ride)]
    [TestCase(20479305419)] // the Evening Run
    [TestCase(1)]
    public async Task Only_a_swim_has_a_swim_detail_page(long id)
    {
        var response = await _browser.GetAsync($"/api/swims/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<SwimPage> ListAsync(string query) =>
        (await _browser.GetFromJsonAsync<SwimPage>($"/api/swims?{query}"))!;

    private sealed record SwimPage(List<SwimItem> Items, int Page, int PageSize, int TotalCount, SwimTotals Totals, List<int> Years)
    {
        public List<string> Names => [.. Items.Select(i => i.Name)];
    }

    private sealed record SwimTotals(int Swims, double Distance, int MovingTime, double? Pace);

    /// <param name="Pace">Seconds per 100 metres.</param>
    private sealed record SwimItem(long Id, string Name, DateTime StartLocal, double Distance, int MovingTime, double? Pace, string StravaUrl);

    /// <param name="Pace">Seconds per 100 metres.</param>
    private sealed record SwimDetail(
        long Id,
        string Name,
        DateTime StartLocal,
        double Distance,
        int MovingTime,
        int ElapsedTime,
        double? Pace,
        double? AverageHeartRate,
        double? MaxHeartRate,
        string StravaUrl);
}
