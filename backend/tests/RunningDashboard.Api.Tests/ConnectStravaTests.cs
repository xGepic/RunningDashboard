using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

public sealed class ConnectStravaTests
{
    private readonly TestApp _app = new();


    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Before_connecting_the_app_is_not_connected()
    {
        var browser = _app.CreateBrowser();

        var connection = await browser.GetConnectionAsync();

        connection.Connected.ShouldBeFalse();
        connection.Athlete.ShouldBeNull();
    }

    [Test]
    public async Task Connect_sends_the_athlete_to_strava_asking_for_read_access_to_activities_and_profile()
    {
        var browser = _app.CreateBrowser();

        var response = await browser.GetAsync("/api/strava/connect");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).ShouldBe("https://www.strava.com/oauth/authorize");
        var query = QueryHelpers.ParseQuery(location.Query);
        query["client_id"].ToString().ShouldBe(TestApp.ClientId);
        query["redirect_uri"].ToString().ShouldBe(TestApp.RedirectUri);
        query["response_type"].ToString().ShouldBe("code");
        query["scope"].ToString().ShouldBe(TestApp.AllScopes);
        string.IsNullOrEmpty(query["state"]).ShouldBeFalse();
    }

    [Test]
    public async Task After_strava_approves_the_app_is_connected_as_that_athlete()
    {
        var browser = _app.CreateBrowser();

        var callback = await _app.ConnectStravaAsync(browser, "token-exchange.json");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.ShouldBe("/?strava=connected");

        var connection = await browser.GetConnectionAsync();
        connection.Connected.ShouldBeTrue();
        connection.Athlete.ShouldBe(new ConnectedAthlete(133415890, "Stefan", "Simanek"));
        connection.ConnectedAt.ShouldBe(_app.Clock.GetUtcNow());
    }

    [Test]
    public async Task The_code_is_exchanged_with_strava_using_the_apps_credentials()
    {
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser, "token-exchange.json");

        // The token request is Strava's contract: a wrong field name here only fails against the real Strava.
        var tokenRequest = _app.Strava.Requests.Where(r => r.Uri.AbsolutePath == "/oauth/token").ShouldHaveSingleItem();
        var form = QueryHelpers.ParseQuery(tokenRequest.Body);
        form["client_id"].ToString().ShouldBe(TestApp.ClientId);
        form["client_secret"].ToString().ShouldBe(TestApp.ClientSecret);
        form["code"].ToString().ShouldBe("auth-code-from-strava");
        form["grant_type"].ToString().ShouldBe("authorization_code");
    }

    [Test]
    public async Task A_different_strava_athlete_is_refused_once_one_is_connected()
    {
        var browser = _app.CreateBrowser();
        await _app.ConnectStravaAsync(browser, "token-exchange.json");

        var callback = await _app.ConnectStravaAsync(browser, "token-exchange-other-athlete.json");

        callback.Headers.Location!.OriginalString.ShouldBe("/?strava=error&reason=different-athlete");
        var connection = await browser.GetConnectionAsync();
        connection.Athlete.ShouldBe(new ConnectedAthlete(133415890, "Stefan", "Simanek"));
    }

    [Test]
    public async Task Connecting_the_same_athlete_again_keeps_the_app_connected()
    {
        var browser = _app.CreateBrowser();
        await _app.ConnectStravaAsync(browser, "token-exchange.json");

        var callback = await _app.ConnectStravaAsync(browser, "token-exchange.json");

        callback.Headers.Location!.OriginalString.ShouldBe("/?strava=connected");
        var connection = await browser.GetConnectionAsync();
        connection.Athlete.ShouldBe(new ConnectedAthlete(133415890, "Stefan", "Simanek"));
    }

    [Test]
    public async Task A_callback_that_was_not_started_by_this_browser_is_refused()
    {
        var browser = _app.CreateBrowser();
        await TestApp.StartConnectAsync(browser);
        _app.Strava.RespondWithFixture(HttpMethod.Post, "/oauth/token", "token-exchange.json");

        var callback = await browser.GetAsync($"/api/strava/callback?state=forged&code=attacker-code&scope={TestApp.AllScopes}");

        callback.Headers.Location!.OriginalString.ShouldBe("/?strava=error&reason=invalid-state");
        (await browser.GetConnectionAsync()).Connected.ShouldBeFalse();
    }

    [Test]
    public async Task Declining_on_strava_leaves_the_app_unconnected()
    {
        var browser = _app.CreateBrowser();
        var state = await TestApp.StartConnectAsync(browser);

        var callback = await browser.GetAsync($"/api/strava/callback?state={state}&error=access_denied");

        callback.Headers.Location!.OriginalString.ShouldBe("/?strava=error&reason=access-denied");
        (await browser.GetConnectionAsync()).Connected.ShouldBeFalse();
    }

    [Test]
    public async Task Unticking_a_required_permission_on_strava_leaves_the_app_unconnected()
    {
        var browser = _app.CreateBrowser();

        var callback = await _app.ConnectStravaAsync(browser, "token-exchange.json", scope: "read,profile:read_all");

        callback.Headers.Location!.OriginalString.ShouldBe("/?strava=error&reason=missing-permissions");
        (await browser.GetConnectionAsync()).Connected.ShouldBeFalse();
    }

    [Test]
    public async Task When_strava_rejects_the_code_the_app_stays_unconnected_and_says_so()
    {
        var browser = _app.CreateBrowser();
        _app.Strava.Respond(HttpMethod.Post, "/oauth/token", () => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { message = "Bad Request", errors = new[] { new { resource = "AuthorizationCode", field = "code", code = "invalid" } } }),
        });
        var state = await TestApp.StartConnectAsync(browser);

        var callback = await browser.GetAsync($"/api/strava/callback?state={state}&code=expired-code&scope={TestApp.AllScopes}");

        callback.Headers.Location!.OriginalString.ShouldBe("/?strava=error&reason=strava-rejected");
        (await browser.GetConnectionAsync()).Connected.ShouldBeFalse();
    }
}
