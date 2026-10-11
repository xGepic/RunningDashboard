using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Three years of 5K Best Efforts: a 5K race, a faster 5K inside a half marathon, a parkrun in exactly the same time
/// and a slower easy run.
/// </summary>
public sealed class RecordsTests : IAsyncLifetime
{
    // As Strava has it.
    private const double HalfMarathon = 21097;

    private static readonly TestActivity CityRace = new(
        1, "City 5K", new DateTime(2024, 4, 14, 9, 0, 0), 5010, 1500,
        BestEfforts: [new("1K", 1000, 290), new("5K", 5000, 1500)]);

    private static readonly TestActivity HalfMarathonRace = new(
        2, "Half marathon", new DateTime(2025, 10, 5, 9, 0, 0), 21150, 6620,
        BestEfforts: [new("400m", 400, 110), new("1K", 1000, 280), new("5K", 5000, 1470), new("10K", 10000, 3020), new("Half-Marathon", HalfMarathon, 6600)]);

    private static readonly TestActivity Parkrun = new(
        3, "Parkrun", new DateTime(2026, 3, 7, 9, 0, 0), 5020, 1470,
        BestEfforts: [new("1K", 1000, 285), new("5K", 5000, 1470)]);

    private static readonly TestActivity EasyRun = new(
        4, "Easy run", new DateTime(2026, 6, 1, 18, 0, 0), 5000, 1620,
        BestEfforts: [new("1K", 1000, 320), new("5K", 5000, 1620)]);

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    public async Task InitializeAsync() =>
        _browser = await _app.ConnectWithActivitiesAsync(CityRace, HalfMarathonRace, Parkrun, EasyRun);

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task A_best_effort_inside_a_longer_run_sets_a_personal_best()
    {
        var fiveK = await GetRecordAsync("5K");

        Assert.Equal(
            [
                new PersonalBestResponse(1, "City 5K", new DateTime(2024, 4, 14, 9, 0, 0), 1500, 300),
                new PersonalBestResponse(2, "Half marathon", new DateTime(2025, 10, 5, 9, 0, 0), 1470, 294),
            ],
            fiveK.Progression);
        Assert.Equal(fiveK.Progression[^1], fiveK.PersonalBest);
    }

    [Fact]
    public async Task An_equal_time_does_not_add_a_step()
    {
        var fiveK = await GetRecordAsync("5K");

        Assert.Equal([1, 2], fiveK.Progression.Select(r => r.RunId));
        Assert.Equal(2, fiveK.PersonalBest.RunId);
    }

    [Fact]
    public async Task Every_distance_strava_provides_is_listed_shortest_first_with_the_main_ones_shown_by_default()
    {
        var records = await GetRecordsAsync();

        Assert.Equal(
            [("400m", false), ("1K", false), ("5K", true), ("10K", false), ("Half-Marathon", true)],
            records.Select(r => (r.Name, r.ShownByDefault)));
        Assert.Equal(HalfMarathon, records[^1].Distance);
    }

    [Fact]
    public async Task A_run_removed_on_strava_disappears_from_the_history()
    {
        var firstSync = await TestApp.WaitForSyncAsync(_browser);
        _app.Clock.Advance(TimeSpan.FromHours(1));
        _app.Strava.RespondWithFixture(HttpMethod.Get, "/api/v3/athlete/activities", "activities-empty.json");
        // The half marathon was deleted on Strava; it is missing from the recent Activities.
        _app.Strava.RespondWithActivities(FakeStrava.RecentActivitiesPath, EasyRun, Parkrun, CityRace);

        await _browser.PostAsync("/api/sync", null);
        await TestApp.WaitForSyncAsync(_browser, finishedAfter: firstSync.LastSyncedAt);
        var records = await GetRecordsAsync();

        Assert.Equal(["1K", "5K"], records.Select(r => r.Name));
        var fiveK = records[1];
        Assert.Equal([1, 3], fiveK.Progression.Select(r => r.RunId));
        Assert.Equal(new PersonalBestResponse(3, "Parkrun", new DateTime(2026, 3, 7, 9, 0, 0), 1470, 294), fiveK.PersonalBest);
    }

    [Fact]
    public async Task A_runs_best_efforts_are_marked_when_they_were_a_personal_best_at_the_time()
    {
        Assert.Equal([("1K", true), ("5K", true)], await PersonalBestMarksAsync(CityRace));
        Assert.Equal(
            [("400m", true), ("1K", true), ("5K", true), ("10K", true), ("Half-Marathon", true)],
            await PersonalBestMarksAsync(HalfMarathonRace));
        Assert.Equal([("1K", false), ("5K", false)], await PersonalBestMarksAsync(Parkrun));
        Assert.Equal([("1K", false), ("5K", false)], await PersonalBestMarksAsync(EasyRun));
    }

    private async Task<List<(string, bool)>> PersonalBestMarksAsync(TestActivity activity)
    {
        var run = (await _browser.GetFromJsonAsync<RunWithBestEfforts>($"/api/runs/{activity.Id}"))!;
        return run.BestEfforts.Select(e => (e.Name, e.PersonalBest)).ToList();
    }

    private async Task<List<RecordDistanceResponse>> GetRecordsAsync() =>
        (await _browser.GetFromJsonAsync<List<RecordDistanceResponse>>("/api/records"))!;

    private async Task<RecordDistanceResponse> GetRecordAsync(string name) =>
        (await GetRecordsAsync()).Single(r => r.Name == name);

    private sealed record RecordDistanceResponse(string Name, double Distance, bool ShownByDefault, List<PersonalBestResponse> Progression, PersonalBestResponse PersonalBest);

    private sealed record PersonalBestResponse(long RunId, string RunName, DateTime Date, int ElapsedTime, double TimePerKm);

    private sealed record RunWithBestEfforts(List<BestEffortResponse> BestEfforts);

    private sealed record BestEffortResponse(string Name, bool PersonalBest);
}
