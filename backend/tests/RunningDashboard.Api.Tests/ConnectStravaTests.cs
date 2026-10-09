using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

public sealed class ConnectStravaTests : IAsyncLifetime
{
    private const string AllScopes = "read,activity:read_all,profile:read_all";

    private readonly TestApp _app = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Before_connecting_the_app_is_not_connected()
    {
        var browser = _app.CreateBrowser();

        var connection = await GetConnectionAsync(browser);

        Assert.False(connection.Connected);
        Assert.Null(connection.Athlete);
    }

    [Fact]
    public async Task Connect_sends_the_athlete_to_strava_asking_for_read_access_to_activities_and_profile()
    {
        var browser = _app.CreateBrowser();

        var response = await browser.GetAsync("/api/strava/connect");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("https://www.strava.com/oauth/authorize", location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal(TestApp.ClientId, query["client_id"]);
        Assert.Equal(TestApp.RedirectUri, query["redirect_uri"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal(AllScopes, query["scope"]);
        Assert.False(string.IsNullOrEmpty(query["state"]));
    }

    [Fact]
    public async Task After_strava_approves_the_app_is_connected_as_that_athlete()
    {
        var browser = _app.CreateBrowser();

        var callback = await ConnectAsync(browser, "token-exchange.json");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/?strava=connected", callback.Headers.Location!.OriginalString);

        var connection = await GetConnectionAsync(browser);
        Assert.True(connection.Connected);
        Assert.Equal(new AthleteResponse(133415890, "Stefan", "Simanek"), connection.Athlete);
        Assert.Equal(_app.Clock.GetUtcNow(), connection.ConnectedAt);
    }

    [Fact]
    public async Task The_code_is_exchanged_with_strava_using_the_apps_credentials()
    {
        var browser = _app.CreateBrowser();

        await ConnectAsync(browser, "token-exchange.json");

        // The token request is Strava's contract: a wrong field name here only fails against the real Strava.
        var tokenRequest = Assert.Single(_app.Strava.Requests, r => r.Uri.AbsolutePath == "/oauth/token");
        var form = QueryHelpers.ParseQuery(tokenRequest.Body);
        Assert.Equal(TestApp.ClientId, form["client_id"]);
        Assert.Equal(TestApp.ClientSecret, form["client_secret"]);
        Assert.Equal("auth-code-from-strava", form["code"]);
        Assert.Equal("authorization_code", form["grant_type"]);
    }

    [Fact]
    public async Task A_different_strava_athlete_is_refused_once_one_is_connected()
    {
        var browser = _app.CreateBrowser();
        await ConnectAsync(browser, "token-exchange.json");

        var callback = await ConnectAsync(browser, "token-exchange-other-athlete.json");

        Assert.Equal("/?strava=error&reason=different-athlete", callback.Headers.Location!.OriginalString);
        var connection = await GetConnectionAsync(browser);
        Assert.Equal(new AthleteResponse(133415890, "Stefan", "Simanek"), connection.Athlete);
    }

    [Fact]
    public async Task Connecting_the_same_athlete_again_keeps_the_app_connected()
    {
        var browser = _app.CreateBrowser();
        await ConnectAsync(browser, "token-exchange.json");

        var callback = await ConnectAsync(browser, "token-exchange.json");

        Assert.Equal("/?strava=connected", callback.Headers.Location!.OriginalString);
        var connection = await GetConnectionAsync(browser);
        Assert.Equal(new AthleteResponse(133415890, "Stefan", "Simanek"), connection.Athlete);
    }

    [Fact]
    public async Task A_callback_that_was_not_started_by_this_browser_is_refused()
    {
        var browser = _app.CreateBrowser();
        await StartConnectAsync(browser);
        _app.Strava.RespondWithFixture(HttpMethod.Post, "/oauth/token", "token-exchange.json");

        var callback = await browser.GetAsync($"/api/strava/callback?state=forged&code=attacker-code&scope={AllScopes}");

        Assert.Equal("/?strava=error&reason=invalid-state", callback.Headers.Location!.OriginalString);
        Assert.False((await GetConnectionAsync(browser)).Connected);
    }

    [Fact]
    public async Task Declining_on_strava_leaves_the_app_unconnected()
    {
        var browser = _app.CreateBrowser();
        var state = await StartConnectAsync(browser);

        var callback = await browser.GetAsync($"/api/strava/callback?state={state}&error=access_denied");

        Assert.Equal("/?strava=error&reason=access-denied", callback.Headers.Location!.OriginalString);
        Assert.False((await GetConnectionAsync(browser)).Connected);
    }

    [Fact]
    public async Task Unticking_a_required_permission_on_strava_leaves_the_app_unconnected()
    {
        var browser = _app.CreateBrowser();

        var callback = await ConnectAsync(browser, "token-exchange.json", scope: "read,profile:read_all");

        Assert.Equal("/?strava=error&reason=missing-permissions", callback.Headers.Location!.OriginalString);
        Assert.False((await GetConnectionAsync(browser)).Connected);
    }

    [Fact]
    public async Task When_strava_rejects_the_code_the_app_stays_unconnected_and_says_so()
    {
        var browser = _app.CreateBrowser();
        _app.Strava.Respond(HttpMethod.Post, "/oauth/token", () => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { message = "Bad Request", errors = new[] { new { resource = "AuthorizationCode", field = "code", code = "invalid" } } }),
        });
        var state = await StartConnectAsync(browser);

        var callback = await browser.GetAsync($"/api/strava/callback?state={state}&code=expired-code&scope={AllScopes}");

        Assert.Equal("/?strava=error&reason=strava-rejected", callback.Headers.Location!.OriginalString);
        Assert.False((await GetConnectionAsync(browser)).Connected);
    }

    /// <summary>
    /// Plays the browser side of the OAuth flow: start at /connect, then come back from Strava with a code.
    /// </summary>
    private async Task<HttpResponseMessage> ConnectAsync(HttpClient browser, string tokenFixture, string scope = AllScopes)
    {
        _app.Strava.RespondWithFixture(HttpMethod.Post, "/oauth/token", tokenFixture);
        var state = await StartConnectAsync(browser);

        return await browser.GetAsync(QueryHelpers.AddQueryString("/api/strava/callback", new Dictionary<string, string?>
        {
            ["state"] = state,
            ["code"] = "auth-code-from-strava",
            ["scope"] = scope,
        }));
    }

    /// <summary>
    /// Clicks "Connect Strava" and returns the state the app expects back from Strava.
    /// </summary>
    private static async Task<string> StartConnectAsync(HttpClient browser)
    {
        var connect = await browser.GetAsync("/api/strava/connect");
        return QueryHelpers.ParseQuery(connect.Headers.Location!.Query)["state"].ToString();
    }

    private static async Task<ConnectionResponse> GetConnectionAsync(HttpClient browser) =>
        (await browser.GetFromJsonAsync<ConnectionResponse>("/api/connection"))!;

    private sealed record ConnectionResponse(bool Connected, AthleteResponse? Athlete, DateTimeOffset? ConnectedAt);

    private sealed record AthleteResponse(long Id, string FirstName, string LastName);
}
