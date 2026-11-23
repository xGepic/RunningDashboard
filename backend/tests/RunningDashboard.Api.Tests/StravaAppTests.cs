using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Without the Strava API app in the user secrets, the Athlete enters their own Strava API app on the Settings page.
/// </summary>
public sealed class StravaAppTests
{
    private readonly TestApp _app = new() { StravaInConfiguration = false };

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Without_a_strava_app_connecting_sends_the_athlete_to_settings_to_enter_one()
    {
        var browser = _app.CreateBrowser();

        var connect = await browser.GetAsync("/api/strava/connect");

        connect.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        connect.Headers.Location!.OriginalString.ShouldBe("/settings?strava=error&reason=no-strava-app");
        (await browser.GetConnectionAsync()).StravaApp.ShouldBeNull();
    }

    [Test]
    public async Task The_strava_app_entered_on_settings_is_the_one_strava_is_connected_with()
    {
        var browser = _app.CreateBrowser();

        var saved = await browser.PutAsJsonAsync("/api/strava/app", new { clientId = " 67890 ", clientSecret = " entered-secret " });

        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await saved.Content.ReadFromJsonAsync<StravaAppResponse>()).ShouldBe(new StravaAppResponse("67890", false));
        (await browser.GetConnectionAsync()).StravaApp.ShouldBe(new StravaAppResponse("67890", false));

        var connect = await browser.GetAsync("/api/strava/connect");
        var query = QueryHelpers.ParseQuery(connect.Headers.Location!.Query);
        query["client_id"].ToString().ShouldBe("67890");
        // No redirect URI is configured: Strava sends the browser back to the host it came from.
        query["redirect_uri"].ToString().ShouldBe("http://localhost/api/strava/callback");

        await _app.ConnectStravaAsync(browser);
        var tokenRequest = _app.Strava.Requests.Where(r => r.Uri.AbsolutePath == "/oauth/token").ShouldHaveSingleItem();
        var form = QueryHelpers.ParseQuery(tokenRequest.Body);
        form["client_id"].ToString().ShouldBe("67890");
        form["client_secret"].ToString().ShouldBe("entered-secret");
        (await browser.GetConnectionAsync()).Connected.ShouldBeTrue();
    }

    [Test]
    public async Task Entering_the_strava_app_again_replaces_it()
    {
        var browser = _app.CreateBrowser();
        await browser.PutAsJsonAsync("/api/strava/app", new { clientId = "67890", clientSecret = "typo" });

        await browser.PutAsJsonAsync("/api/strava/app", new { clientId = "67891", clientSecret = "entered-secret" });

        await _app.ConnectStravaAsync(browser);
        var form = QueryHelpers.ParseQuery(_app.Strava.Requests.Single(r => r.Uri.AbsolutePath == "/oauth/token").Body);
        form["client_id"].ToString().ShouldBe("67891");
        form["client_secret"].ToString().ShouldBe("entered-secret");
    }

    [Test]
    public async Task A_strava_app_without_a_numeric_client_id_or_a_secret_is_refused()
    {
        var browser = _app.CreateBrowser();

        var response = await browser.PutAsJsonAsync("/api/strava/app", new { clientId = "my app", clientSecret = " " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        problem!.Errors.Keys.ShouldBe(["clientId", "clientSecret"], ignoreOrder: true);
        (await browser.GetConnectionAsync()).StravaApp.ShouldBeNull();
    }

    [Test]
    public async Task The_strava_app_in_configuration_wins_over_the_one_entered_on_settings()
    {
        await using var app = new TestApp();
        var browser = app.CreateBrowser();
        (await browser.GetConnectionAsync()).StravaApp.ShouldBe(new StravaAppResponse(TestApp.ClientId, true));

        await browser.PutAsJsonAsync("/api/strava/app", new { clientId = "67890", clientSecret = "entered-secret" });

        (await browser.GetConnectionAsync()).StravaApp.ShouldBe(new StravaAppResponse(TestApp.ClientId, true));
    }
}
