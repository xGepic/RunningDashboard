using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Like Runs, Rides among the 20 most recent Activities are rechecked on every sync (ADR 0002). The scenario: since
/// the first import, the Athlete, on Strava, renamed the Kahlenberg climb, deleted the Zwift Watopia ride, turned the
/// Commute ride into a Run and the Evening Run into a Ride.
/// </summary>
public sealed class RideSyncTests
{
    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    private static readonly TestActivity KahlenbergClimb = new(20460000001, "Kahlenberg climb", new DateTime(2026, 10, 5, 10, 0, 0), 42300, 5400, "Ride");
    private static readonly TestActivity EveningRun = new(20450000007, "Evening Run", new DateTime(2026, 10, 3, 19, 0, 0), 5000, 1800);
    private static readonly TestActivity ZwiftWatopia = new(20430000003, "Zwift Watopia", new DateTime(2026, 10, 1, 19, 0, 0), 30000, 3600, "VirtualRide");
    private static readonly TestActivity Commute = new(20425000008, "Commute", new DateTime(2026, 9, 30, 7, 30, 0), 6000, 1500, "Ride");
    private static readonly TestActivity GravelMarchfeld = new(20410000005, "Gravel Marchfeld", new DateTime(2026, 9, 20, 9, 0, 0), 61200, 9000, "GravelRide");

    [SetUp]
    public async Task SetUpAsync()
    {
        _browser = await _app.ConnectWithActivitiesAsync(KahlenbergClimb, EveningRun, ZwiftWatopia, Commute, GravelMarchfeld);
        var firstSync = await TestApp.WaitForSyncAsync(_browser);

        _app.Clock.Advance(TimeSpan.FromHours(1));
        _app.Strava.RespondWithFixture(HttpMethod.Get, "/api/v3/athlete/activities", "activities-empty.json");
        _app.Strava.RespondWithActivities(
            FakeStrava.RecentActivitiesPath,
            KahlenbergClimb with { Name = "Kahlenberg climb in the rain" },
            EveningRun with { SportType = "Ride" },
            Commute with { SportType = "Run" });
        await _browser.PostAsync("/api/sync", null);
        await TestApp.WaitForSyncAsync(_browser, finishedAfter: firstSync.LastSyncedAt);
    }

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task A_renamed_ride_gets_its_new_name()
    {
        (await RideNamesAsync()).ShouldContain("Kahlenberg climb in the rain");
    }

    [Test]
    public async Task A_ride_deleted_on_strava_is_removed()
    {
        (await RideNamesAsync()).ShouldNotContain("Zwift Watopia");
    }

    [Test]
    public async Task A_ride_older_than_the_recent_activities_stays_untouched()
    {
        (await RideNamesAsync()).ShouldContain("Gravel Marchfeld");
    }

    [Test]
    public async Task A_ride_changed_to_a_run_moves_to_the_runs()
    {
        (await RideNamesAsync()).ShouldNotContain("Commute");
        (await NamesAsync("/api/runs")).ShouldContain("Commute");
    }

    [Test]
    public async Task A_run_changed_to_a_ride_moves_to_the_rides()
    {
        (await RideNamesAsync()).ShouldContain("Evening Run");
        (await NamesAsync("/api/runs")).ShouldNotContain("Evening Run");
    }

    private Task<List<string>> RideNamesAsync() => NamesAsync("/api/rides");

    private async Task<List<string>> NamesAsync(string list) =>
        [.. (await _browser.GetFromJsonAsync<Page>(list))!.Items.Select(i => i.Name)];

    private sealed record Page(List<Item> Items);

    private sealed record Item(string Name);
}
