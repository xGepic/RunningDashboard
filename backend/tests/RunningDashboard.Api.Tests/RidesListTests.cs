using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Four Rides among other Activities, synced once and shared, since these tests only read:
///
/// | Ride             | Type             | Date       | Distance | Moving Time | Avg Speed   | Elevation |
/// |------------------|------------------|------------|----------|-------------|-------------|-----------|
/// | Kahlenberg climb | Ride             | 2026-10-05 | 42,300 m |   5,400 s   | = 28.2 km/h |   620 m   |
/// | Zwift Watopia    | VirtualRide      | 2026-10-01 | 30,000 m |   3,600 s   | = 30.0 km/h |   310 m   |
/// | Gravel Marchfeld | GravelRide       | 2026-09-26 | 61,200 m |   9,000 s   | = 24.48 km/h|   180 m   |
/// | Anninger MTB     | MountainBikeRide | 2026-09-20 | 24,000 m |   5,760 s   | = 15.0 km/h |   740 m   |
///
/// Not Rides: an Evening Run, an e-bike ride and a walk.
/// </summary>
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public sealed class RidesListTests
{
    private const long KahlenbergClimb = 20460000001;

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync() => _browser = await _app.ConnectWithActivitiesAsync(
        new TestActivity(20479305419, "Evening Run", new DateTime(2026, 10, 6, 20, 35, 26), 5081.2, 2096),
        new TestActivity(KahlenbergClimb, "Kahlenberg climb", new DateTime(2026, 10, 5, 10, 0, 0), 42300, 5400, "Ride", ElevationGain: 620, ElapsedTime: 6100),
        new TestActivity(20440000002, "E-bike shopping", new DateTime(2026, 10, 2, 11, 0, 0), 8000, 1500, "EBikeRide"),
        new TestActivity(20430000003, "Zwift Watopia", new DateTime(2026, 10, 1, 19, 0, 0), 30000, 3600, "VirtualRide", ElevationGain: 310),
        new TestActivity(20420000004, "Sunday walk", new DateTime(2026, 9, 27, 15, 0, 0), 4000, 3000, "Walk"),
        new TestActivity(20410000005, "Gravel Marchfeld", new DateTime(2026, 9, 26, 9, 0, 0), 61200, 9000, "GravelRide", ElevationGain: 180),
        new TestActivity(20400000006, "Anninger MTB", new DateTime(2026, 9, 20, 14, 0, 0), 24000, 5760, "MountainBikeRide", ElevationGain: 740));

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Road_virtual_gravel_and_mountain_bike_rides_are_listed_newest_first_and_nothing_else()
    {
        var page = await ListAsync("");

        page.Names.ShouldBe(["Kahlenberg climb", "Zwift Watopia", "Gravel Marchfeld", "Anninger MTB"]);
        page.TotalCount.ShouldBe(4);
    }

    [Test]
    public async Task A_ride_is_listed_with_its_avg_speed_elevation_gain_and_link_to_strava()
    {
        var ride = (await ListAsync("")).Items[0];

        (ride.Id, ride.SportType, ride.StartLocal).ShouldBe((KahlenbergClimb, "Ride", new DateTime(2026, 10, 5, 10, 0, 0)));
        (ride.Distance, ride.MovingTime, ride.ElevationGain).ShouldBe((42300, 5400, 620));
        ride.Speed!.Value.ShouldBe(28.2, 0.0005); // 42.3 km in 1.5 h
        ride.StravaUrl.ShouldBe($"https://www.strava.com/activities/{KahlenbergClimb}");
    }

    [TestCase("zwift", new[] { "Zwift Watopia" })]
    [TestCase("MTB", new[] { "Anninger MTB" })]
    [TestCase("Evening", new string[0])]
    public async Task Searching_matches_part_of_the_name_ignoring_case(string search, string[] expected)
    {
        (await ListAsync($"search={search}")).Names.ShouldBe(expected);
    }

    [Test]
    public async Task A_date_range_includes_both_days()
    {
        var page = await ListAsync("from=2026-09-26&to=2026-10-01");

        page.Names.ShouldBe(["Zwift Watopia", "Gravel Marchfeld"]);
        page.TotalCount.ShouldBe(2);
    }

    [TestCase("date", "asc", new[] { "Anninger MTB", "Gravel Marchfeld", "Zwift Watopia", "Kahlenberg climb" })]
    [TestCase("distance", "desc", new[] { "Gravel Marchfeld", "Kahlenberg climb", "Zwift Watopia", "Anninger MTB" })]
    [TestCase("distance", "asc", new[] { "Anninger MTB", "Zwift Watopia", "Kahlenberg climb", "Gravel Marchfeld" })]
    [TestCase("movingTime", "desc", new[] { "Gravel Marchfeld", "Anninger MTB", "Kahlenberg climb", "Zwift Watopia" })]
    [TestCase("movingTime", "asc", new[] { "Zwift Watopia", "Kahlenberg climb", "Anninger MTB", "Gravel Marchfeld" })]
    [TestCase("speed", "desc", new[] { "Zwift Watopia", "Kahlenberg climb", "Gravel Marchfeld", "Anninger MTB" })]
    [TestCase("speed", "asc", new[] { "Anninger MTB", "Gravel Marchfeld", "Kahlenberg climb", "Zwift Watopia" })]
    [TestCase("elevation", "desc", new[] { "Anninger MTB", "Kahlenberg climb", "Zwift Watopia", "Gravel Marchfeld" })]
    [TestCase("elevation", "asc", new[] { "Gravel Marchfeld", "Zwift Watopia", "Kahlenberg climb", "Anninger MTB" })]
    public async Task Rides_sort_by_each_key_in_both_directions(string sort, string direction, string[] expected)
    {
        (await ListAsync($"sort={sort}&direction={direction}")).Names.ShouldBe(expected);
    }

    [TestCase(1, new[] { "Kahlenberg climb", "Zwift Watopia", "Gravel Marchfeld" })]
    [TestCase(2, new[] { "Anninger MTB" })]
    [TestCase(3, new string[0])]
    public async Task Pages_split_the_list_and_report_the_total(int pageNumber, string[] expected)
    {
        var page = await ListAsync($"page={pageNumber}&pageSize=3");

        page.Names.ShouldBe(expected);
        (page.TotalCount, page.Page, page.PageSize).ShouldBe((4, pageNumber, 3));
    }

    [TestCase("page=0")]
    [TestCase("pageSize=0")]
    [TestCase("pageSize=201")]
    [TestCase("sort=pace")]
    [TestCase("direction=up")]
    [TestCase("from=2026-10-01&to=2026-09-26")]
    public async Task Invalid_options_are_refused(string query)
    {
        var response = await _browser.GetAsync($"/api/rides?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<RidePage> ListAsync(string query) =>
        (await _browser.GetFromJsonAsync<RidePage>($"/api/rides?{query}"))!;

    private sealed record RidePage(List<RideItem> Items, int Page, int PageSize, int TotalCount)
    {
        public List<string> Names => [.. Items.Select(i => i.Name)];
    }

    /// <param name="Speed">Kilometres per hour.</param>
    private sealed record RideItem(
        long Id, string Name, string SportType, DateTime StartLocal, double Distance, int MovingTime, double? Speed, double ElevationGain, string StravaUrl);
}
