using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Strava lists three Shoes: the Nike Pegasus 41 (Evening Run, Lauf am Nachmittag, Treadmill intervals), the Adidas
/// Adizero SL2 (Finisher., Wienerwald trail) and the retired Asics Novablast 4, which none of the synced Runs used.
/// </summary>
public sealed class ShoesTests : IAsyncLifetime
{
    private const string ActivitiesPath = "/api/v3/athlete/activities";
    private const string AthletePath = "/api/v3/athlete";

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    public async Task InitializeAsync()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-2.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, AthletePath, "athlete.json");
        _browser = _app.CreateBrowser();
        await _app.ConnectStravaAsync(_browser);
        await TestApp.WaitForSyncAsync(_browser);
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task The_sync_brings_in_every_shoe_with_its_strava_distance_runs_and_last_run()
    {
        var shoes = await GetShoesAsync();

        Assert.Equal(
            [
                new ShoeResponse("g31444377", "Nike Pegasus 41", 812345, false, 3, new DateTime(2026, 10, 6, 20, 35, 26)),
                new ShoeResponse("g33395946", "Adidas Adizero SL2", 412000, false, 2, new DateTime(2026, 9, 27, 9, 47, 0)),
                new ShoeResponse("g29876543", "Asics Novablast 4", 1012000, true, 0, null),
            ],
            shoes);
    }

    [Fact]
    public async Task A_shoe_retired_on_strava_is_retired_here_and_listed_after_the_active_ones()
    {
        var firstSync = await TestApp.WaitForSyncAsync(_browser);
        _app.Clock.Advance(TimeSpan.FromHours(1));
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, AthletePath, "athlete-shoe-retired.json");

        await _browser.PostAsync("/api/sync", null);
        await TestApp.WaitForSyncAsync(_browser, finishedAfter: firstSync.LastSyncedAt);
        var shoes = await GetShoesAsync();

        Assert.Equal(["Adidas Adizero SL2", "Nike Pegasus 41", "Asics Novablast 4"], shoes.Select(s => s.Name));
        var pegasus = shoes[1];
        Assert.True(pegasus.Retired);
        Assert.Equal(820123, pegasus.Distance);
    }

    [Fact]
    public async Task Shoes_deleted_on_strava_are_removed_and_their_runs_show_no_shoe()
    {
        var firstSync = await TestApp.WaitForSyncAsync(_browser);
        _app.Clock.Advance(TimeSpan.FromHours(1));
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, AthletePath, "athlete-without-shoes.json");

        await _browser.PostAsync("/api/sync", null);
        await TestApp.WaitForSyncAsync(_browser, finishedAfter: firstSync.LastSyncedAt);
        var list = (await _browser.GetFromJsonAsync<RunPage>("/api/runs"))!;

        Assert.Empty(await GetShoesAsync());
        Assert.All(list.Items, r => Assert.Null(r.ShoeName));
    }

    [Fact]
    public async Task Runs_show_the_name_of_their_shoe()
    {
        var list = (await _browser.GetFromJsonAsync<RunPage>("/api/runs"))!;
        var marathon = (await _browser.GetFromJsonAsync<RunWithShoe>("/api/runs/20351282630"))!;

        Assert.Equal("Nike Pegasus 41", list.Items.Single(r => r.Name == "Evening Run").ShoeName);
        Assert.Equal("Adidas Adizero SL2", list.Items.Single(r => r.Name == "Wienerwald trail").ShoeName);
        Assert.Equal("Adidas Adizero SL2", marathon.ShoeName);
    }

    private async Task<List<ShoeResponse>> GetShoesAsync() =>
        (await _browser.GetFromJsonAsync<List<ShoeResponse>>("/api/shoes"))!;

    private sealed record ShoeResponse(string Id, string Name, double Distance, bool Retired, int Runs, DateTime? LastRun);

    private sealed record RunPage(List<RunWithShoe> Items);

    private sealed record RunWithShoe(string Name, string? ShoeName);
}
