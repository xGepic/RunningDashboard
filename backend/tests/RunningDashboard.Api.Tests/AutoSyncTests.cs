using System.Net;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// While the backend runs, it syncs by itself every hour (Sync:Interval), so a Run uploaded to Strava shows up
/// without "Sync now".
/// </summary>
public sealed class AutoSyncTests
{
    private const string ActivitiesPath = "/api/v3/athlete/activities";

    private static readonly TestActivity EveningRun = new(20412345678, "Evening Run", new DateTime(2026, 10, 8, 18, 30, 0), 8000, 2880);
    private static readonly TestActivity MorningRun = new(20512345678, "Morning Run", new DateTime(2026, 10, 9, 7, 0, 0), 6000, 2160);

    private TestApp _app = new() { SyncInterval = TimeSpan.FromHours(1) };

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task A_run_uploaded_after_the_last_sync_is_imported_within_the_hour()
    {
        var browser = await _app.ConnectWithActivitiesAsync(EveningRun);
        var firstSync = await TestApp.WaitForSyncAsync(browser);
        _app.Strava.RespondWithActivities(ActivitiesPath, MorningRun);
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");

        _app.Clock.Advance(TimeSpan.FromHours(1));
        await TestApp.WaitForSyncAsync(browser, finishedAfter: firstSync.LastSyncedAt);

        (await browser.GetAsync($"/api/runs/{MorningRun.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task No_sync_starts_by_itself_before_the_hour_is_up()
    {
        var browser = await _app.ConnectWithActivitiesAsync(EveningRun);

        _app.Clock.Advance(TimeSpan.FromMinutes(59));

        await NoSyncStartsAsync(browser);
    }

    [Test]
    public async Task No_sync_starts_by_itself_while_Strava_needs_to_be_reconnected()
    {
        var browser = await _app.ConnectWithActivitiesAsync(EveningRun);
        _app.Strava.Respond(HttpMethod.Get, ActivitiesPath, () => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        _app.Strava.Respond(HttpMethod.Post, "/oauth/token", () => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        await browser.PostAsync("/api/sync", null);
        await TestApp.WaitForStatusAsync(browser, s => !s.Running && s.ReconnectRequired);

        _app.Clock.Advance(TimeSpan.FromHours(1));

        await NoSyncStartsAsync(browser);
    }

    [Test]
    public async Task Syncing_by_itself_can_be_turned_off()
    {
        await _app.DisposeAsync();
        _app = new TestApp { SyncInterval = TimeSpan.Zero };
        var browser = await _app.ConnectWithActivitiesAsync(EveningRun);

        _app.Clock.Advance(TimeSpan.FromHours(3));

        await NoSyncStartsAsync(browser);
    }

    /// <summary>Waits a moment (real time) and checks that no sync started in it.</summary>
    private async Task NoSyncStartsAsync(HttpClient browser)
    {
        var requests = _app.Strava.Requests.Count;

        await Task.Delay(300);

        _app.Strava.Requests.Count.ShouldBe(requests);
        (await TestApp.WaitForStatusAsync(browser, _ => true)).Running.ShouldBeFalse();
    }
}
