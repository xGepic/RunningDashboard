using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

public sealed class ImportRunsTests : IAsyncLifetime
{
    private const string ActivitiesPath = "/api/v3/athlete/activities";

    private readonly TestApp _app = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Connecting_imports_only_the_runs_from_every_page_of_activities_newest_first()
    {
        StravaHasTwoPagesOfActivities();
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        await TestApp.WaitForSyncAsync(browser);

        var runs = await GetRunsAsync(browser);
        Assert.Equal(
            ["Evening Run", "Lauf am Nachmittag", "Finisher.", "Treadmill intervals", "Wienerwald trail"],
            runs.Select(r => r.Name));

        var eveningRun = runs[0];
        Assert.Equal(20479305419, eveningRun.Id);
        Assert.Equal("Run", eveningRun.SportType);
        Assert.Equal(new DateTime(2026, 10, 6, 20, 35, 26), eveningRun.StartLocal);
        Assert.Equal(5081.2, eveningRun.Distance);
        Assert.Equal(2096, eveningRun.MovingTime);
        Assert.Equal(412.5, eveningRun.Pace, 1); // 2096 s over 5.0812 km ≈ 6:52.5 /km
    }

    [Fact]
    public async Task Activities_are_listed_with_the_athletes_access_token()
    {
        StravaHasTwoPagesOfActivities();
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        await TestApp.WaitForSyncAsync(browser);

        var listRequests = _app.Strava.Requests.Where(r => r.Uri.AbsolutePath == ActivitiesPath).ToList();
        Assert.NotEmpty(listRequests);
        Assert.All(listRequests, r => Assert.Equal("Bearer a4b945687g1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e", r.Authorization));
    }

    [Fact]
    public async Task Syncing_again_does_not_duplicate_runs()
    {
        StravaHasTwoPagesOfActivities();
        StravaHasTwoPagesOfActivities();
        var browser = _app.CreateBrowser();
        await _app.ConnectStravaAsync(browser);
        var firstSync = await TestApp.WaitForSyncAsync(browser);
        _app.Clock.Advance(TimeSpan.FromHours(1));

        var syncNow = await browser.PostAsync("/api/sync", null);
        await TestApp.WaitForSyncAsync(browser, finishedAfter: firstSync.LastSyncedAt);

        Assert.Equal(HttpStatusCode.Accepted, syncNow.StatusCode);
        Assert.Equal(5, (await GetRunsAsync(browser)).Count);
    }

    [Fact]
    public async Task Sync_now_during_a_running_sync_reports_the_running_sync_instead_of_starting_another()
    {
        var releaseFirstPage = new TaskCompletionSource();
        _app.Strava.RespondWithFixtureAfter(releaseFirstPage.Task, HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-2.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var browser = _app.CreateBrowser();
        await _app.ConnectStravaAsync(browser);

        var syncNow = await browser.PostAsync("/api/sync", null);
        var status = await syncNow.Content.ReadFromJsonAsync<SyncStatusResponse>();
        releaseFirstPage.SetResult();
        await TestApp.WaitForSyncAsync(browser);

        Assert.True(status!.Running);
        Assert.Single(_app.Strava.Requests, r => r.Uri.AbsolutePath == ActivitiesPath && r.Uri.Query.Contains("per_page=200&") && !r.Uri.Query.Contains("before="));
        Assert.Equal(5, (await GetRunsAsync(browser)).Count);
    }

    [Fact]
    public async Task The_backend_syncs_when_it_starts_with_strava_connected()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"running-dashboard-test-{Guid.NewGuid():N}.db");
        try
        {
            await using (var before = new TestApp(databasePath))
            {
                before.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
                var firstBrowser = before.CreateBrowser();
                await before.ConnectStravaAsync(firstBrowser);
                await TestApp.WaitForSyncAsync(firstBrowser);
            }

            await using var restarted = new TestApp(databasePath);
            restarted.Clock.Advance(TimeSpan.FromHours(1));
            restarted.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
            restarted.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");

            var browser = restarted.CreateBrowser();
            await TestApp.WaitForSyncAsync(browser, finishedAfter: restarted.Clock.GetUtcNow().AddMinutes(-1));

            Assert.Equal(["Evening Run", "Lauf am Nachmittag"], (await GetRunsAsync(browser)).Select(r => r.Name));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task Runs_show_up_while_the_import_is_still_running()
    {
        var releaseSecondPage = new TaskCompletionSource();
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
        _app.Strava.RespondWithFixtureAfter(releaseSecondPage.Task, HttpMethod.Get, ActivitiesPath, "activities-page-2.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        var midImport = await TestApp.WaitForStatusAsync(browser, s => s.ImportedRuns == 2);
        var runsMidImport = await GetRunsAsync(browser);
        releaseSecondPage.SetResult();
        var finished = await TestApp.WaitForSyncAsync(browser);

        Assert.True(midImport.Running);
        Assert.Equal("importing-runs", midImport.Phase);
        Assert.Equal(["Evening Run", "Lauf am Nachmittag"], runsMidImport.Select(r => r.Name));
        Assert.Equal(5, finished.ImportedRuns);
        Assert.Equal("idle", finished.Phase);
    }

    [Fact]
    public async Task An_interrupted_import_continues_after_a_restart_without_fetching_the_imported_activities_again()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"running-dashboard-test-{Guid.NewGuid():N}.db");
        try
        {
            await using (var interrupted = new TestApp(databasePath))
            {
                interrupted.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
                interrupted.Strava.RespondWithFixtureAfter(new TaskCompletionSource().Task, HttpMethod.Get, ActivitiesPath, "activities-page-2.json");
                var firstBrowser = interrupted.CreateBrowser();
                await interrupted.ConnectStravaAsync(firstBrowser);
                await TestApp.WaitForStatusAsync(firstBrowser, s => s.ImportedRuns == 2);
            }

            await using var restarted = new TestApp(databasePath);
            restarted.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-2.json");
            restarted.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
            var browser = restarted.CreateBrowser();
            var finished = await TestApp.WaitForSyncAsync(browser);

            // Continues from the oldest Activity of the first page (Evening Walk, 2026-10-01T17:02:10Z); Strava's
            // "before" is exclusive, so one second later keeps Activities from that same second.
            var firstRequestAfterRestart = restarted.Strava.Requests.First(r => r.Uri.AbsolutePath == ActivitiesPath);
            Assert.Contains("before=1790874131", firstRequestAfterRestart.Uri.Query);
            Assert.Equal(5, finished.ImportedRuns);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task Without_any_runs_yet_later_syncs_still_fetch_only_activities_after_the_newest_one()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-no-runs.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var browser = _app.CreateBrowser();
        await _app.ConnectStravaAsync(browser);
        var firstSync = await TestApp.WaitForSyncAsync(browser);
        _app.Clock.Advance(TimeSpan.FromHours(1));
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var requestsBefore = _app.Strava.Requests.Count;

        await browser.PostAsync("/api/sync", null);
        await TestApp.WaitForSyncAsync(browser, finishedAfter: firstSync.LastSyncedAt);

        // One second before the Morning Ride (2026-10-05T07:12:03Z), the newest Activity, though not a Run.
        var newActivitiesRequest = _app.Strava.Requests.Skip(requestsBefore).First(r => r.Uri.AbsolutePath == ActivitiesPath);
        Assert.Contains("after=1791184322", newActivitiesRequest.Uri.Query);
    }

    [Fact]
    public async Task Sync_now_before_connecting_strava_is_refused()
    {
        var browser = _app.CreateBrowser();

        var syncNow = await browser.PostAsync("/api/sync", null);

        Assert.Equal(HttpStatusCode.Conflict, syncNow.StatusCode);
    }

    private void StravaHasTwoPagesOfActivities()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-2.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
    }

    private static async Task<List<RunResponse>> GetRunsAsync(HttpClient browser) =>
        (await browser.GetFromJsonAsync<RunPage>("/api/runs"))!.Items;

    private sealed record RunPage(List<RunResponse> Items);

    private sealed record RunResponse(long Id, string Name, string SportType, DateTime StartLocal, double Distance, int MovingTime, double Pace);
}
