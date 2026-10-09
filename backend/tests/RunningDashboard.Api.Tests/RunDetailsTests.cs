using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

public sealed class RunDetailsTests : IAsyncLifetime
{
    private const string ActivitiesPath = "/api/v3/athlete/activities";
    private const long EveningRun = 20479305419;
    private const long LaufAmNachmittag = 20436100848;

    private readonly TestApp _app = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task The_sync_stores_each_runs_best_efforts_and_splits_for_its_detail_page()
    {
        StravaHasTheFirstPageOfActivities(_app);
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(EveningRun), $"activity-{EveningRun}.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(LaufAmNachmittag), $"activity-{LaufAmNachmittag}.json");
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        var finished = await TestApp.WaitForSyncAsync(browser);
        var run = (await browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{EveningRun}"))!;

        Assert.Equal(2, finished.DetailedRuns);
        Assert.Equal("loaded", run.Detail);
        Assert.Equal("Evening Run", run.Name);
        Assert.Equal(5081.2, run.Distance);
        Assert.Equal(2096, run.MovingTime);
        Assert.Equal(2096, run.ElapsedTime);
        Assert.Equal(11, run.ElevationGain);
        Assert.Equal(16, run.RelativeEffort);
        Assert.Equal("https://www.strava.com/activities/20479305419", run.StravaUrl);

        Assert.Equal(["400m", "1/2 mile", "1K", "1 mile", "2 mile", "5K"], run.BestEfforts.Select(e => e.Name));
        var fiveK = run.BestEfforts[^1];
        Assert.Equal(5000, fiveK.Distance);
        Assert.Equal(2058, fiveK.ElapsedTime);
        Assert.Equal(411.6, fiveK.TimePerKm!.Value, 1); // 2058 s over 5 km = 6:51.6 /km

        Assert.Equal([1, 2, 3, 4, 5, 6], run.Splits.Select(s => s.Index));
        Assert.Equal(new SplitResponse(1, 1000, 410, 410, 1.2), run.Splits[0]);
        var lastSplit = run.Splits[^1];
        Assert.Equal(81.2, lastSplit.Distance);
        Assert.Equal(33, lastSplit.MovingTime);
        Assert.Equal(406.4, lastSplit.Pace, 1); // 33 s over 81.2 m = 6:46.4 /km
    }

    [Fact]
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

        Assert.True(midDetails.Running);
        Assert.Equal("importing-details", midDetails.Phase);
        Assert.Equal(2, midDetails.ImportedRuns);
        Assert.Equal(2, finished.DetailedRuns);
    }

    [Fact]
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

            Assert.Equal(2, finished.DetailedRuns);
            Assert.DoesNotContain(restarted.Strava.Requests, r => r.Uri.AbsolutePath == DetailPath(EveningRun));
            var lauf = (await browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{LaufAmNachmittag}"))!;
            Assert.Equal(5, lauf.Splits.Count);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task A_run_strava_no_longer_has_does_not_hold_up_the_sync()
    {
        StravaHasTheFirstPageOfActivities(_app);
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(EveningRun), $"activity-{EveningRun}.json");
        // No response for Lauf am Nachmittag: the fake answers 404 like Strava does for a deleted Activity.
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        var finished = await TestApp.WaitForSyncAsync(browser);
        var lauf = (await browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{LaufAmNachmittag}"))!;

        Assert.Null(finished.LastError);
        Assert.Equal(2, finished.DetailedRuns);
        Assert.Equal("unavailable", lauf.Detail);
        Assert.Empty(lauf.BestEfforts);
        Assert.Equal(1885, lauf.MovingTime);
    }

    [Fact]
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

        Assert.Equal(1, finished.DetailedRuns);
        Assert.Contains("details of 1 Run", finished.LastError);
        Assert.Equal("pending", eveningRun.Detail);
        Assert.Equal("loaded", lauf.Detail);
    }

    [Fact]
    public async Task A_failed_shoe_refresh_does_not_hold_up_the_details_and_is_reported()
    {
        StravaHasTheFirstPageOfActivities(_app);
        _app.Strava.Respond(HttpMethod.Get, "/api/v3/athlete", () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(EveningRun), $"activity-{EveningRun}.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(LaufAmNachmittag), $"activity-{LaufAmNachmittag}.json");
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        var finished = await TestApp.WaitForStatusAsync(browser, s => !s.Running && s.LastError is not null);

        Assert.Equal(2, finished.DetailedRuns);
        Assert.Contains("Shoes", finished.LastError);
    }

    [Fact]
    public async Task An_unknown_run_is_not_found()
    {
        var browser = _app.CreateBrowser();

        var response = await browser.GetAsync("/api/runs/12345");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        List<BestEffortResponse> BestEfforts,
        List<SplitResponse> Splits);

    private sealed record BestEffortResponse(string Name, double Distance, int ElapsedTime, double? TimePerKm);

    private sealed record SplitResponse(int Index, double Distance, int MovingTime, double Pace, double ElevationDifference);
}
