using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Tests.Harness;

/// <summary>
/// The real app in an in-memory host with its own SQLite file. Only Strava (HTTP) and the clock are fake.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    public const string ClientId = "12345";
    public const string ClientSecret = "test-client-secret";
    public const string RedirectUri = "http://localhost:4200/api/strava/callback";
    public const string AllScopes = "read,activity:read_all,profile:read_all";

    private readonly bool _ownsDatabase;

    /// <param name="databasePath">Reuse another TestApp's database to simulate a backend restart.</param>
    public TestApp(string? databasePath = null)
    {
        _ownsDatabase = databasePath is null;
        DatabasePath = databasePath ?? Path.Combine(Path.GetTempPath(), $"running-dashboard-test-{Guid.NewGuid():N}.db");
    }

    public string DatabasePath { get; }

    public FakeStrava Strava { get; } = new();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 9, 18, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:RunningDashboard", $"Data Source={DatabasePath};Pooling=False");
        builder.UseSetting("Strava:ClientId", ClientId);
        builder.UseSetting("Strava:ClientSecret", ClientSecret);
        builder.UseSetting("Strava:RedirectUri", RedirectUri);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddHttpClient<StravaClient>().ConfigurePrimaryHttpMessageHandler(() => Strava);
        });
    }

    public HttpClient CreateBrowser() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>
    /// Plays the browser side of the OAuth flow: click "Connect Strava", then come back from Strava with a code.
    /// </summary>
    public async Task<HttpResponseMessage> ConnectStravaAsync(HttpClient browser, string tokenFixture = "token-exchange.json", string scope = AllScopes)
    {
        Strava.RespondWithFixture(HttpMethod.Post, "/oauth/token", tokenFixture);
        var state = await StartConnectAsync(browser);

        return await browser.GetAsync(QueryHelpers.AddQueryString("/api/strava/callback", new Dictionary<string, string?>
        {
            ["state"] = state,
            ["code"] = "auth-code-from-strava",
            ["scope"] = scope,
        }));
    }

    /// <summary>
    /// Connects Strava and waits for the first sync, which imports exactly <paramref name="activities"/>.
    /// </summary>
    public async Task<HttpClient> ConnectWithActivitiesAsync(params TestActivity[] activities)
    {
        Strava.RespondWithActivities("/api/v3/athlete/activities", activities);
        Strava.RespondWithFixture(HttpMethod.Get, "/api/v3/athlete/activities", "activities-empty.json");
        var browser = CreateBrowser();
        await ConnectStravaAsync(browser);
        await WaitForSyncAsync(browser);
        return browser;
    }

    /// <summary>
    /// Clicks "Connect Strava" and returns the state the app expects back from Strava.
    /// </summary>
    public static async Task<string> StartConnectAsync(HttpClient browser)
    {
        var connect = await browser.GetAsync("/api/strava/connect");
        return QueryHelpers.ParseQuery(connect.Headers.Location!.Query)["state"].ToString();
    }

    /// <summary>
    /// Waits until no sync is running and one finished (after <paramref name="finishedAfter"/>, if given).
    /// </summary>
    public static Task<SyncStatusResponse> WaitForSyncAsync(HttpClient browser, DateTimeOffset? finishedAfter = null) =>
        WaitForStatusAsync(browser, status =>
            !status.Running && status.LastSyncedAt is { } finished && (finishedAfter is null || finished > finishedAfter));

    /// <summary>
    /// Polls the sync status until <paramref name="condition"/> holds (real time; the fake clock does not move).
    /// </summary>
    public static async Task<SyncStatusResponse> WaitForStatusAsync(HttpClient browser, Func<SyncStatusResponse, bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            var status = (await browser.GetFromJsonAsync<SyncStatusResponse>("/api/sync"))!;
            if (condition(status))
                return status;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Sync status never matched: {status}");
            await Task.Delay(20);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_ownsDatabase)
            File.Delete(DatabasePath);
    }
}

public sealed record SyncStatusResponse(
    bool Running,
    DateTimeOffset? LastSyncedAt,
    string? Phase = null,
    int ImportedRuns = 0,
    int DetailedRuns = 0,
    DateTimeOffset? PausedUntil = null,
    string? LastError = null,
    bool ReconnectRequired = false);
