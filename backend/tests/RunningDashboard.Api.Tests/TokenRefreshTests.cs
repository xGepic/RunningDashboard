using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

public sealed class TokenRefreshTests
{
    private const string ActivitiesPath = "/api/v3/athlete/activities";
    private const string ConnectAccessToken = "Bearer a4b945687g1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e";
    private const string RefreshedAccessToken = "Bearer c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";

    // token-exchange.json's access token expires at 2026-10-10T06:00Z, 12 hours after the clock starts.
    private static readonly DateTimeOffset AccessTokenExpiry = new(2026, 10, 10, 6, 0, 0, TimeSpan.Zero);

    private readonly TestApp _app = new();


    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
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

        var refresh = _app.Strava.Requests.Where(r => r.Uri.AbsolutePath == "/oauth/token" && r.Body!.Contains("refresh_token")).ShouldHaveSingleItem();
        var form = QueryHelpers.ParseQuery(refresh.Body);
        form["grant_type"].ToString().ShouldBe("refresh_token");
        form["refresh_token"].ToString().ShouldBe("e5n567567a8f9b0c1d2e3f4a5b6c7d8e9f0a1b2c");
        form["client_id"].ToString().ShouldBe(TestApp.ClientId);
        form["client_secret"].ToString().ShouldBe(TestApp.ClientSecret);
        var refreshAt = _app.Strava.Requests.IndexOf(refresh);
        var listTokens = _app.Strava.Requests.Select((r, i) => (Request: r, Index: i)).Where(x => x.Request.Uri.AbsolutePath == ActivitiesPath).ToList();
        listTokens.Where(x => x.Index < refreshAt).ShouldAllBe(x => x.Request.Authorization == ConnectAccessToken);
        listTokens.Where(x => x.Index > refreshAt).ShouldAllBe(x => x.Request.Authorization == RefreshedAccessToken);
        listTokens.ShouldContain(x => x.Index > refreshAt);
    }

    [Test]
    public async Task A_revoked_token_makes_the_sync_status_ask_for_a_reconnect()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var browser = _app.CreateBrowser();
        await ConnectAndSyncAsync(browser);
        StravaHasRevokedAccess();

        await browser.PostAsync("/api/sync", null);
        var status = await TestApp.WaitForStatusAsync(browser, s => !s.Running && s.ReconnectRequired);

        status.LastError.ShouldNotBeNull();
    }

    [Test]
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

    [Test]
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

        status.ReconnectRequired.ShouldBeFalse();
        status.LastError.ShouldBeNull();
        status.ImportedRuns.ShouldBe(5);
    }

    [Test]
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

        status.ReconnectRequired.ShouldBeFalse();
        status.ImportedRuns.ShouldBe(2);
        _app.Strava.Requests.Last(r => r.Uri.AbsolutePath == ActivitiesPath).Authorization.ShouldBe(RefreshedAccessToken);
    }

    [Test]
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

        status.ReconnectRequired.ShouldBeFalse();
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
