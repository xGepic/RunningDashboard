using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>The Heatmap: every Run's simplified route, to draw on one map.</summary>
public sealed class HeatmapTests
{
    /// <summary>Google's polyline example, standing in for real routes.</summary>
    private const string Route = "_p~iF~ps|U_ulLnnqC";

    private const string OtherRoute = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";

    private readonly TestApp _app = new();

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Lists_every_run_with_a_route_outdoors_oldest_first()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Prater loop", new DateTime(2026, 3, 1, 9, 0, 0), 10000, 3000, SummaryRoute: OtherRoute),
            new TestActivity(2, "Trail", new DateTime(2025, 6, 1, 8, 0, 0), 15000, 6000, "TrailRun", SummaryRoute: Route),
            new TestActivity(3, "Without GPS", new DateTime(2026, 3, 2, 9, 0, 0), 5000, 1500),
            // A treadmill can still record a route, e.g. where the watch picked up GPS; it says nothing about where the Athlete ran.
            new TestActivity(4, "Treadmill", new DateTime(2026, 3, 3, 9, 0, 0), 8000, 2400, Trainer: true, SummaryRoute: Route),
            new TestActivity(5, "Zwift run", new DateTime(2026, 3, 4, 9, 0, 0), 8000, 2400, "VirtualRun", SummaryRoute: Route),
            new TestActivity(6, "Danube Ride", new DateTime(2026, 3, 5, 9, 0, 0), 60000, 7200, "Ride", SummaryRoute: Route));

        var heatmap = (await browser.GetFromJsonAsync<HeatmapResponse>("/api/heatmap"))!;

        heatmap.Runs.ShouldBe([
            new HeatmapRun(2, "Trail", new DateTime(2025, 6, 1, 8, 0, 0), 15000, Route),
            new HeatmapRun(1, "Prater loop", new DateTime(2026, 3, 1, 9, 0, 0), 10000, OtherRoute),
        ]);
    }

    [Test]
    public async Task Is_empty_without_runs()
    {
        var browser = await _app.ConnectWithActivitiesAsync();

        var heatmap = (await browser.GetFromJsonAsync<HeatmapResponse>("/api/heatmap"))!;

        heatmap.Runs.ShouldBeEmpty();
    }

    private sealed record HeatmapResponse(HeatmapRun[] Runs);

    private sealed record HeatmapRun(long Id, string Name, DateTime StartLocal, double Distance, string Route);
}
