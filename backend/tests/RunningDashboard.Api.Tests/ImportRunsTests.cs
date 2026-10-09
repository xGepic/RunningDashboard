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
        Assert.Single(_app.Strava.Requests, r => r.Uri.AbsolutePath == ActivitiesPath && r.Uri.Query.Contains("page=1&"));
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
            restarted.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
            restarted.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");

            var browser = restarted.CreateBrowser();
            await TestApp.WaitForSyncAsync(browser);

            Assert.Equal(["Evening Run", "Lauf am Nachmittag"], (await GetRunsAsync(browser)).Select(r => r.Name));
        }
        finally
        {
            File.Delete(databasePath);
        }
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
        (await browser.GetFromJsonAsync<List<RunResponse>>("/api/runs"))!;

    private sealed record RunResponse(long Id, string Name, string SportType, DateTime StartLocal, double Distance, int MovingTime, double Pace);
}
