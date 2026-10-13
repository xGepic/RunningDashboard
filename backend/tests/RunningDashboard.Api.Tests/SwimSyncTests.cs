using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Like Runs and Rides, Swims among the 20 most recent Activities are rechecked on every sync (ADR 0002). The scenario:
/// since the first import, the Athlete, on Strava, renamed the Pool 2k, deleted the Open water swim and turned the
/// Aqua jogging swim into a Run.
/// </summary>
public sealed class SwimSyncTests
{
    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    private static readonly TestActivity Pool2K = new(20460000011, "Pool 2k", new DateTime(2026, 10, 5, 7, 0, 0), 2000, 2400, "Swim");
    private static readonly TestActivity OpenWater = new(20430000013, "Open water", new DateTime(2026, 10, 1, 18, 0, 0), 1500, 2550, "Swim");
    private static readonly TestActivity AquaJogging = new(20425000018, "Aqua jogging", new DateTime(2026, 9, 30, 7, 30, 0), 1000, 1800, "Swim");
    private static readonly TestActivity Lake3K = new(20400000016, "Lake 3k", new DateTime(2026, 9, 20, 9, 0, 0), 3000, 3150, "Swim");

    [SetUp]
    public async Task SetUpAsync()
    {
        _browser = await _app.ConnectWithActivitiesAsync(Pool2K, OpenWater, AquaJogging, Lake3K);
        var firstSync = await TestApp.WaitForSyncAsync(_browser);

        _app.Clock.Advance(TimeSpan.FromHours(1));
        _app.Strava.RespondWithFixture(HttpMethod.Get, "/api/v3/athlete/activities", "activities-empty.json");
        _app.Strava.RespondWithActivities(
            FakeStrava.RecentActivitiesPath,
            Pool2K with { Name = "Pool 2k, 50 m lane" },
            AquaJogging with { SportType = "Run" });
        await _browser.PostAsync("/api/sync", null);
        await TestApp.WaitForSyncAsync(_browser, finishedAfter: firstSync.LastSyncedAt);
    }

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task A_renamed_swim_gets_its_new_name()
    {
        (await NamesAsync("/api/swims")).ShouldContain("Pool 2k, 50 m lane");
    }

    [Test]
    public async Task A_swim_deleted_on_strava_is_removed()
    {
        (await NamesAsync("/api/swims")).ShouldNotContain("Open water");
    }

    [Test]
    public async Task A_swim_older_than_the_recent_activities_stays_untouched()
    {
        (await NamesAsync("/api/swims")).ShouldContain("Lake 3k");
    }

    [Test]
    public async Task A_swim_changed_to_a_run_moves_to_the_runs()
    {
        (await NamesAsync("/api/swims")).ShouldNotContain("Aqua jogging");
        (await NamesAsync("/api/runs")).ShouldContain("Aqua jogging");
    }

    private async Task<List<string>> NamesAsync(string list) =>
        [.. (await _browser.GetFromJsonAsync<Page>(list))!.Items.Select(i => i.Name)];

    private sealed record Page(List<Item> Items);

    private sealed record Item(string Name);
}
