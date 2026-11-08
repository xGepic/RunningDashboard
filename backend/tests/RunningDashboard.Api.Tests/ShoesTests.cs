using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Strava lists three Shoes: the Nike Pegasus 41 (Evening Run, Lauf am Nachmittag, Treadmill intervals), the Adidas
/// Adizero SL2 (Finisher., Wienerwald trail) and the retired Asics Novablast 4, which none of the synced Runs used.
/// </summary>
public sealed class ShoesTests
{
    private const string ActivitiesPath = "/api/v3/athlete/activities";
    private const string AthletePath = "/api/v3/athlete";

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-2.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, AthletePath, "athlete.json");
        _browser = _app.CreateBrowser();
        await _app.ConnectStravaAsync(_browser);
        await TestApp.WaitForSyncAsync(_browser);
    }

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task The_sync_brings_in_every_shoe_with_its_strava_distance_runs_and_last_run()
    {
        var shoes = await GetShoesAsync();

        shoes.ShouldBe(
            [
                new ShoeResponse("g31444377", "Nike Pegasus 41", 812345, false, 3, new DateTime(2026, 10, 6, 20, 35, 26)),
                new ShoeResponse("g33395946", "Adidas Adizero SL2", 412000, false, 2, new DateTime(2026, 9, 27, 9, 47, 0)),
                new ShoeResponse("g29876543", "Asics Novablast 4", 1012000, true, 0, null),
            ]);
    }

    [Test]
    public async Task A_shoes_recent_distance_counts_its_runs_of_the_last_five_weeks_today_included()
    {
        // Sunday 25 Oct: the five weeks start on Monday 21 Sep, the day of the Wienerwald trail.
        _app.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 25, 18, 0, 0, TimeSpan.Zero));
        var lastDayIn = (await _browser.GetFromJsonAsync<List<ShoeRecentDistance>>("/api/shoes"))!;
        _app.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 26, 18, 0, 0, TimeSpan.Zero));
        var dayAfter = (await _browser.GetFromJsonAsync<List<ShoeRecentDistance>>("/api/shoes"))!;

        lastDayIn.Single(s => s.Name == "Adidas Adizero SL2").RecentDistance.ShouldBe(42582.8 + 12105.4, 0.01);
        dayAfter.Single(s => s.Name == "Adidas Adizero SL2").RecentDistance.ShouldBe(42582.8, 0.01);
        dayAfter.Single(s => s.Name == "Nike Pegasus 41").RecentDistance.ShouldBe(5081.2 + 4513.7 + 6000, 0.01);
        dayAfter.Single(s => s.Name == "Asics Novablast 4").RecentDistance.ShouldBe(0);
    }

    [Test]
    public async Task A_shoe_retired_on_strava_is_retired_here_and_listed_after_the_active_ones()
    {
        await SyncAgainAsync("athlete-shoe-retired.json");

        var shoes = await GetShoesAsync();

        shoes.Select(s => s.Name).ShouldBe(["Adidas Adizero SL2", "Nike Pegasus 41", "Asics Novablast 4"]);
        var pegasus = shoes[1];
        pegasus.Retired.ShouldBeTrue();
        pegasus.Distance.ShouldBe(820123);
    }

    [Test]
    public async Task Shoes_strava_no_longer_lists_are_retired_here_and_their_runs_keep_their_shoe()
    {
        // Strava leaves retired (and deleted) Shoes out of the athlete's list.
        await SyncAgainAsync("athlete-without-shoes.json");
        var list = await _browser.ListRunsAsync();

        (await GetShoesAsync()).ShouldBe(
            [
                new ShoeResponse("g31444377", "Nike Pegasus 41", 812345, true, 3, new DateTime(2026, 10, 6, 20, 35, 26)),
                new ShoeResponse("g33395946", "Adidas Adizero SL2", 412000, true, 2, new DateTime(2026, 9, 27, 9, 47, 0)),
                new ShoeResponse("g29876543", "Asics Novablast 4", 1012000, true, 0, null),
            ]);
        list.Items.Single(r => r.Name == "Evening Run").ShoeName.ShouldBe("Nike Pegasus 41");
    }

    [Test]
    public async Task A_retired_shoe_strava_lists_again_is_active_again()
    {
        await SyncAgainAsync("athlete-without-shoes.json");
        await SyncAgainAsync("athlete.json");

        var shoes = await GetShoesAsync();

        shoes.Single(s => s.Name == "Nike Pegasus 41").Retired.ShouldBeFalse();
        shoes.Single(s => s.Name == "Adidas Adizero SL2").Retired.ShouldBeFalse();
    }

    [Test]
    public async Task Runs_show_the_name_of_their_shoe()
    {
        var list = await _browser.ListRunsAsync();
        var marathon = await _browser.GetRunAsync(20351282630);

        list.Items.Single(r => r.Name == "Evening Run").ShoeName.ShouldBe("Nike Pegasus 41");
        list.Items.Single(r => r.Name == "Wienerwald trail").ShoeName.ShouldBe("Adidas Adizero SL2");
        marathon.ShoeName.ShouldBe("Adidas Adizero SL2");
    }

    [Test]
    public async Task The_runs_of_a_shoe_are_the_ones_logged_with_it_newest_first()
    {
        var pegasus = await _browser.ListRunsAsync("shoe=g31444377");
        var adizero = await _browser.ListRunsAsync("shoe=g33395946&sort=distance&direction=asc");
        var novablast = await _browser.ListRunsAsync("shoe=g29876543");

        pegasus.Items.Select(r => r.Name).ShouldBe(["Evening Run", "Lauf am Nachmittag", "Treadmill intervals"]);
        adizero.Items.Select(r => r.Name).ShouldBe(["Wienerwald trail", "Finisher."]);
        novablast.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task A_shoes_export_holds_only_its_runs()
    {
        var runs = (await _browser.GetFromJsonAsync<List<ExportedRun>>("/api/runs/export?shoe=g33395946"))!;

        runs.ShouldBe([new ExportedRun("Finisher.", "Adidas Adizero SL2"), new ExportedRun("Wienerwald trail", "Adidas Adizero SL2")]);
    }

    /// <summary>Syncs an hour later, with no new Activities and the athlete's Shoes as in the fixture.</summary>
    private async Task SyncAgainAsync(string athleteFixture)
    {
        var lastSync = await TestApp.WaitForSyncAsync(_browser);
        _app.Clock.Advance(TimeSpan.FromHours(1));
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, AthletePath, athleteFixture);

        await _browser.PostAsync("/api/sync", null);
        await TestApp.WaitForSyncAsync(_browser, finishedAfter: lastSync.LastSyncedAt);
    }

    private async Task<List<ShoeResponse>> GetShoesAsync() =>
        (await _browser.GetFromJsonAsync<List<ShoeResponse>>("/api/shoes"))!;

    private sealed record ShoeResponse(string Id, string Name, double Distance, bool Retired, int Runs, DateTime? LastRun);

    private sealed record ShoeRecentDistance(string Name, double RecentDistance);

    /// <summary>A Run as /api/runs/export lists it.</summary>
    private sealed record ExportedRun(string Name, string? ShoeName);
}
