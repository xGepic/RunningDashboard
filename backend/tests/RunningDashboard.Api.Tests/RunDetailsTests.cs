using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

public sealed class RunDetailsTests
{
    private const string ActivitiesPath = "/api/v3/athlete/activities";
    private const long EveningRun = 20479305419;
    private const long LaufAmNachmittag = 20436100848;

    /// <summary>The Evening Run's routes: Google's polyline example, standing in for a real one.</summary>
    private const string FullRoute = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";

    private const string SummaryRoute = "_p~iF~ps|U_ulLnnqC";

    private readonly TestApp _app = new();


    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task The_sync_stores_each_runs_best_efforts_and_splits_for_its_detail_page()
    {
        StravaHasTheFirstPageOfActivities(_app);
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(EveningRun), $"activity-{EveningRun}.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(LaufAmNachmittag), $"activity-{LaufAmNachmittag}.json");
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        var finished = await TestApp.WaitForSyncAsync(browser);
        var run = (await browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{EveningRun}"))!;

        finished.DetailedRuns.ShouldBe(2);
        run.Detail.ShouldBe("loaded");
        run.Name.ShouldBe("Evening Run");
        run.Distance.ShouldBe(5081.2);
        run.MovingTime.ShouldBe(2096);
        run.ElapsedTime.ShouldBe(2096);
        run.ElevationGain.ShouldBe(11);
        run.RelativeEffort.ShouldBe(16);
        run.StravaUrl.ShouldBe("https://www.strava.com/activities/20479305419");

        run.BestEfforts.Select(e => e.Name).ShouldBe(["400m", "1/2 mile", "1K", "1 mile", "2 mile", "5K"]);
        var fiveK = run.BestEfforts[^1];
        fiveK.Distance.ShouldBe(5000);
        fiveK.ElapsedTime.ShouldBe(2058);
        fiveK.TimePerKm!.Value.ShouldBe(411.6, 0.05); // 2058 s over 5 km = 6:51.6 /km

        run.Splits.Select(s => s.Index).ShouldBe([1, 2, 3, 4, 5, 6]);
        run.Splits[0].ShouldBe(new SplitResponse(1, 1000, 410, 410, 1.2));
        var lastSplit = run.Splits[^1];
        lastSplit.Distance.ShouldBe(81.2);
        lastSplit.MovingTime.ShouldBe(33);
        lastSplit.Pace.ShouldBe(406.4, 0.05); // 33 s over 81.2 m = 6:46.4 /km
    }

    [Test]
    public async Task While_details_load_the_status_shows_how_many_runs_are_detailed()
    {
        var releaseSecondDetail = new TaskCompletionSource();
        StravaHasTheFirstPageOfActivities(_app);
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(EveningRun), $"activity-{EveningRun}.json");
        _app.Strava.RespondWithFixtureAfter(releaseSecondDetail.Task, HttpMethod.Get, DetailPath(LaufAmNachmittag), $"activity-{LaufAmNachmittag}.json");
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        var midDetails = await TestApp.WaitForStatusAsync(browser, s => s.DetailedRuns == 1);
        releaseSecondDetail.SetResult();
        var finished = await TestApp.WaitForSyncAsync(browser);

        midDetails.Running.ShouldBeTrue();
        midDetails.Phase.ShouldBe("importing-details");
        midDetails.ImportedRuns.ShouldBe(2);
        finished.DetailedRuns.ShouldBe(2);
    }

    [Test]
    public async Task An_interrupted_sync_fetches_only_the_details_still_missing_after_a_restart()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"running-dashboard-test-{Guid.NewGuid():N}.db");
        try
        {
            await using (var interrupted = new TestApp(databasePath))
            {
                StravaHasTheFirstPageOfActivities(interrupted);
                interrupted.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(EveningRun), $"activity-{EveningRun}.json");
                interrupted.Strava.RespondWithFixtureAfter(new TaskCompletionSource().Task, HttpMethod.Get, DetailPath(LaufAmNachmittag), $"activity-{LaufAmNachmittag}.json");
                var firstBrowser = interrupted.CreateBrowser();
                await interrupted.ConnectStravaAsync(firstBrowser);
                await TestApp.WaitForStatusAsync(firstBrowser, s => s.DetailedRuns == 1);
            }

            await using var restarted = new TestApp(databasePath);
            StravaHasTheFirstPageOfActivities(restarted);
            restarted.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(LaufAmNachmittag), $"activity-{LaufAmNachmittag}.json");
            var browser = restarted.CreateBrowser();
            var finished = await TestApp.WaitForSyncAsync(browser);

            finished.DetailedRuns.ShouldBe(2);
            restarted.Strava.Requests.ShouldNotContain(r => r.Uri.AbsolutePath == DetailPath(EveningRun));
            var lauf = (await browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{LaufAmNachmittag}"))!;
            lauf.Splits.Count.ShouldBe(5);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Test]
    public async Task A_run_strava_no_longer_has_does_not_hold_up_the_sync()
    {
        StravaHasTheFirstPageOfActivities(_app);
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(EveningRun), $"activity-{EveningRun}.json");
        // No response for Lauf am Nachmittag: the fake answers 404 like Strava does for a deleted Activity.
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        var finished = await TestApp.WaitForSyncAsync(browser);
        var lauf = (await browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{LaufAmNachmittag}"))!;

        finished.LastError.ShouldBeNull();
        finished.DetailedRuns.ShouldBe(2);
        lauf.Detail.ShouldBe("unavailable");
        lauf.BestEfforts.ShouldBeEmpty();
        lauf.MovingTime.ShouldBe(1885);
    }

    [Test]
    public async Task A_run_whose_detail_fails_to_load_does_not_hold_up_the_older_runs()
    {
        StravaHasTheFirstPageOfActivities(_app);
        _app.Strava.Respond(HttpMethod.Get, DetailPath(EveningRun), () => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(LaufAmNachmittag), $"activity-{LaufAmNachmittag}.json");
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        var finished = await TestApp.WaitForStatusAsync(browser, s => !s.Running && s.LastError is not null);
        var eveningRun = (await browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{EveningRun}"))!;
        var lauf = (await browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{LaufAmNachmittag}"))!;

        finished.DetailedRuns.ShouldBe(1);
        finished.LastError.ShouldNotBeNull().ShouldContain("details of 1 Run");
        eveningRun.Detail.ShouldBe("pending");
        lauf.Detail.ShouldBe("loaded");
    }

    [Test]
    public async Task A_failed_shoe_refresh_does_not_hold_up_the_details_and_is_reported()
    {
        StravaHasTheFirstPageOfActivities(_app);
        _app.Strava.Respond(HttpMethod.Get, "/api/v3/athlete", () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(EveningRun), $"activity-{EveningRun}.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(LaufAmNachmittag), $"activity-{LaufAmNachmittag}.json");
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        var finished = await TestApp.WaitForStatusAsync(browser, s => !s.Running && s.LastError is not null);

        finished.DetailedRuns.ShouldBe(2);
        finished.LastError.ShouldNotBeNull().ShouldContain("Shoes");
    }

    [Test]
    public async Task A_run_has_the_full_route_of_its_detail_and_one_without_gps_has_none()
    {
        StravaHasTheFirstPageOfActivities(_app);
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(EveningRun), $"activity-{EveningRun}.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(LaufAmNachmittag), $"activity-{LaufAmNachmittag}.json");
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        await TestApp.WaitForSyncAsync(browser);
        var withRoute = (await browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{EveningRun}"))!;
        var withoutRoute = (await browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{LaufAmNachmittag}"))!;

        withRoute.Route.ShouldBe(FullRoute);
        withoutRoute.Route.ShouldBeNull();
    }

    [Test]
    public async Task While_its_detail_is_still_to_come_a_run_has_the_simplified_route_of_its_summary()
    {
        var releaseDetail = new TaskCompletionSource();
        StravaHasTheFirstPageOfActivities(_app);
        _app.Strava.RespondWithFixtureAfter(releaseDetail.Task, HttpMethod.Get, DetailPath(EveningRun), $"activity-{EveningRun}.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(LaufAmNachmittag), $"activity-{LaufAmNachmittag}.json");
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        await TestApp.WaitForStatusAsync(browser, s => s.Phase == "importing-details");
        var pending = (await browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{EveningRun}"))!;
        releaseDetail.SetResult();
        await TestApp.WaitForSyncAsync(browser);

        pending.Detail.ShouldBe("pending");
        pending.Route.ShouldBe(SummaryRoute);
    }

    [Test]
    public async Task An_unknown_run_is_not_found()
    {
        var browser = _app.CreateBrowser();

        var response = await browser.GetAsync("/api/runs/12345");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static string DetailPath(long activityId) => $"/api/v3/activities/{activityId}";

    /// <summary>Evening Run and Lauf am Nachmittag, plus a ride and a walk.</summary>
    private static void StravaHasTheFirstPageOfActivities(TestApp app)
    {
        app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
        app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
    }

    private sealed record RunDetailResponse(
        long Id,
        string Name,
        double Distance,
        int MovingTime,
        int ElapsedTime,
        double? Pace,
        double ElevationGain,
        double? RelativeEffort,
        string StravaUrl,
        string Detail,
        string? Route,
        List<BestEffortResponse> BestEfforts,
        List<SplitResponse> Splits);

    private sealed record BestEffortResponse(string Name, double Distance, int ElapsedTime, double? TimePerKm);

    private sealed record SplitResponse(int Index, double Distance, int MovingTime, double Pace, double ElevationDifference);
}
