using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Strava allows 100 read requests per 15 minutes (windows start at :00, :15, :30, :45) and 1,000 per UTC day,
/// and reports usage in X-ReadRateLimit-* / X-RateLimit-* headers on every response.
/// </summary>
public sealed class RateLimitTests : IAsyncLifetime
{
    private const string ActivitiesPath = "/api/v3/athlete/activities";
    private static readonly DateTimeOffset SevenPastSix = new(2026, 10, 9, 18, 7, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset QuarterPastSix = new(2026, 10, 9, 18, 15, 0, TimeSpan.Zero);

    private readonly TestApp _app = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task A_rate_limited_request_pauses_the_sync_until_the_next_quarter_hour_and_then_resumes()
    {
        _app.Clock.SetUtcNow(SevenPastSix);
        _app.Strava.Respond(HttpMethod.Get, ActivitiesPath, () => WithReadUsage(TooManyRequests(), fifteenMinutes: 101, day: 350));
        StravaHasTwoPagesOfActivities();
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        var paused = await TestApp.WaitForStatusAsync(browser, s => s.PausedUntil is not null);
        var listRequestsWhilePaused = ListRequests().Count;
        _app.Clock.Advance(QuarterPastSix - SevenPastSix);
        var finished = await TestApp.WaitForSyncAsync(browser);

        Assert.Equal(QuarterPastSix, paused.PausedUntil);
        Assert.True(paused.Running);
        Assert.Equal(1, listRequestsWhilePaused);
        Assert.Null(finished.PausedUntil);
        Assert.Equal(5, finished.ImportedRuns);
    }

    [Fact]
    public async Task The_sync_waits_for_the_next_quarter_hour_instead_of_exceeding_the_fifteen_minute_limit()
    {
        _app.Clock.SetUtcNow(SevenPastSix);
        _app.Strava.Respond(HttpMethod.Get, ActivitiesPath, () => WithReadUsage(FakeStrava.FixtureResponse("activities-page-1.json"), fifteenMinutes: 100, day: 350));
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-2.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        var paused = await TestApp.WaitForStatusAsync(browser, s => s.PausedUntil is not null);
        var listRequestsWhilePaused = ListRequests().Count;
        _app.Clock.Advance(QuarterPastSix - SevenPastSix);
        var finished = await TestApp.WaitForSyncAsync(browser);

        Assert.Equal(QuarterPastSix, paused.PausedUntil);
        Assert.Equal(1, listRequestsWhilePaused);
        Assert.Equal(5, finished.ImportedRuns);
    }

    [Fact]
    public async Task Once_the_daily_limit_is_used_up_the_sync_waits_until_midnight_utc()
    {
        _app.Clock.SetUtcNow(SevenPastSix);
        _app.Strava.Respond(HttpMethod.Get, ActivitiesPath, () => WithReadUsage(FakeStrava.FixtureResponse("activities-page-1.json"), fifteenMinutes: 12, day: 1000));
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        var browser = _app.CreateBrowser();

        await _app.ConnectStravaAsync(browser);
        var paused = await TestApp.WaitForStatusAsync(browser, s => s.PausedUntil is not null);
        _app.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero));
        var finished = await TestApp.WaitForSyncAsync(browser);

        Assert.Equal(new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero), paused.PausedUntil);
        Assert.Equal(2, finished.ImportedRuns);
    }

    private List<RecordedRequest> ListRequests() =>
        [.. _app.Strava.Requests.Where(r => r.Uri.AbsolutePath == ActivitiesPath)];

    private void StravaHasTwoPagesOfActivities()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-2.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
    }

    private static HttpResponseMessage TooManyRequests() => new(HttpStatusCode.TooManyRequests)
    {
        Content = JsonContent.Create(new { message = "Rate Limit Exceeded", errors = new[] { new { resource = "Application", field = "rate limit", code = "exceeded" } } }),
    };

    /// <summary>Adds Strava's rate-limit headers for read usage of 100 per 15 minutes and 1,000 per day.</summary>
    private static HttpResponseMessage WithReadUsage(HttpResponseMessage response, int fifteenMinutes, int day)
    {
        response.Headers.Add("X-RateLimit-Limit", "200,2000");
        response.Headers.Add("X-RateLimit-Usage", $"{fifteenMinutes},{day}");
        response.Headers.Add("X-ReadRateLimit-Limit", "100,1000");
        response.Headers.Add("X-ReadRateLimit-Usage", $"{fifteenMinutes},{day}");
        return response;
    }
}
