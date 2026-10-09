using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

public sealed class TokenRefreshTests : IAsyncLifetime
{
    private const string ActivitiesPath = "/api/v3/athlete/activities";
    private const string ConnectAccessToken = "Bearer a4b945687g1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e";
    private const string RefreshedAccessToken = "Bearer c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";

    // token-exchange.json's access token expires at 2026-10-10T06:00Z, 12 hours after the clock starts.
    private static readonly DateTimeOffset AccessTokenExpiry = new(2026, 10, 10, 6, 0, 0, TimeSpan.Zero);

    private readonly TestApp _app = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task A_token_about_to_expire_is_refreshed_before_syncing_and_the_new_one_is_kept()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var browser = _app.CreateBrowser();
        var connected = await ConnectAndSyncAsync(browser);
        _app.Clock.SetUtcNow(AccessTokenExpiry.AddMinutes(-2));
        _app.Strava.RespondWithFixture(HttpMethod.Post, "/oauth/token", "token-refresh.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");

        await browser.PostAsync("/api/sync", null);
        var refreshed = await TestApp.WaitForSyncAsync(browser, finishedAfter: connected.LastSyncedAt);
        _app.Clock.Advance(TimeSpan.FromMinutes(10));
        await browser.PostAsync("/api/sync", null);
        await TestApp.WaitForSyncAsync(browser, finishedAfter: refreshed.LastSyncedAt);

        var refresh = Assert.Single(_app.Strava.Requests, r => r.Uri.AbsolutePath == "/oauth/token" && r.Body!.Contains("refresh_token"));
        var form = QueryHelpers.ParseQuery(refresh.Body);
        Assert.Equal("refresh_token", form["grant_type"]);
        Assert.Equal("e5n567567a8f9b0c1d2e3f4a5b6c7d8e9f0a1b2c", form["refresh_token"]);
        Assert.Equal(TestApp.ClientId, form["client_id"]);
        Assert.Equal(TestApp.ClientSecret, form["client_secret"]);
        Assert.Equal(
            [ConnectAccessToken, RefreshedAccessToken, RefreshedAccessToken],
            _app.Strava.Requests.Where(r => r.Uri.AbsolutePath == ActivitiesPath).Select(r => r.Authorization));
    }

    [Fact]
    public async Task A_revoked_token_makes_the_sync_status_ask_for_a_reconnect()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var browser = _app.CreateBrowser();
        await ConnectAndSyncAsync(browser);
        StravaHasRevokedAccess();

        await browser.PostAsync("/api/sync", null);
        var status = await TestApp.WaitForStatusAsync(browser, s => !s.Running && s.ReconnectRequired);

        Assert.NotNull(status.LastError);
    }

    [Fact]
    public async Task A_refused_token_refresh_makes_the_sync_status_ask_for_a_reconnect()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var browser = _app.CreateBrowser();
        await ConnectAndSyncAsync(browser);
        _app.Clock.SetUtcNow(AccessTokenExpiry.AddMinutes(1));
        _app.Strava.Respond(HttpMethod.Post, "/oauth/token", () => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"message":"Bad Request","errors":[{"resource":"RefreshToken","field":"refresh_token","code":"invalid"}]}"""),
        });

        await browser.PostAsync("/api/sync", null);

        await TestApp.WaitForStatusAsync(browser, s => !s.Running && s.ReconnectRequired);
    }

    [Fact]
    public async Task Reconnecting_the_same_athlete_syncs_again_and_keeps_the_runs()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var browser = _app.CreateBrowser();
        var firstSync = await ConnectAndSyncAsync(browser);
        StravaHasRevokedAccess();
        await browser.PostAsync("/api/sync", null);
        await TestApp.WaitForStatusAsync(browser, s => !s.Running && s.ReconnectRequired);
        _app.Clock.Advance(TimeSpan.FromMinutes(5));
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-2.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");

        await _app.ConnectStravaAsync(browser);
        var status = await TestApp.WaitForSyncAsync(browser, finishedAfter: firstSync.LastSyncedAt);

        Assert.False(status.ReconnectRequired);
        Assert.Null(status.LastError);
        Assert.Equal(5, status.ImportedRuns);
    }

    [Fact]
    public async Task A_refused_access_token_is_refreshed_once_and_the_sync_carries_on()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var browser = _app.CreateBrowser();
        var firstSync = await ConnectAndSyncAsync(browser);
        _app.Clock.Advance(TimeSpan.FromMinutes(5));
        _app.Strava.Respond(HttpMethod.Get, ActivitiesPath, StravaUnauthorized);
        _app.Strava.RespondWithFixture(HttpMethod.Post, "/oauth/token", "token-refresh.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");

        await browser.PostAsync("/api/sync", null);
        var status = await TestApp.WaitForSyncAsync(browser, finishedAfter: firstSync.LastSyncedAt);

        Assert.False(status.ReconnectRequired);
        Assert.Equal(2, status.ImportedRuns);
        Assert.Equal(RefreshedAccessToken, _app.Strava.Requests.Last(r => r.Uri.AbsolutePath == ActivitiesPath).Authorization);
    }

    [Fact]
    public async Task A_sync_that_succeeds_again_clears_the_reconnect_request()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var browser = _app.CreateBrowser();
        var firstSync = await ConnectAndSyncAsync(browser);
        StravaHasRevokedAccess();
        await browser.PostAsync("/api/sync", null);
        await TestApp.WaitForStatusAsync(browser, s => !s.Running && s.ReconnectRequired);
        _app.Clock.Advance(TimeSpan.FromMinutes(5));
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");

        await browser.PostAsync("/api/sync", null);
        var status = await TestApp.WaitForSyncAsync(browser, finishedAfter: firstSync.LastSyncedAt);

        Assert.False(status.ReconnectRequired);
    }

    /// <summary>The access token is refused, and so is refreshing it: what a revoked app looks like.</summary>
    private void StravaHasRevokedAccess()
    {
        _app.Strava.Respond(HttpMethod.Get, ActivitiesPath, StravaUnauthorized);
        _app.Strava.Respond(HttpMethod.Post, "/oauth/token", () => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"message":"Authorization Error","errors":[{"resource":"RefreshToken","field":"refresh_token","code":"invalid"}]}"""),
        });
    }

    private static HttpResponseMessage StravaUnauthorized() => new(HttpStatusCode.Unauthorized)
    {
        Content = new StringContent("""{"message":"Authorization Error","errors":[{"resource":"Athlete","field":"access_token","code":"invalid"}]}"""),
    };

    private async Task<SyncStatusResponse> ConnectAndSyncAsync(HttpClient browser)
    {
        await _app.ConnectStravaAsync(browser);
        return await TestApp.WaitForSyncAsync(browser);
    }
}
