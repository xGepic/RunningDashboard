using System.Net;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// After the first import, a sync fetches only what is new and rechecks the 20 most recent Activities (ADR 0002).
/// The scenario: since the first import, the Athlete ran a Morning Run and, on Strava, renamed the Evening Run,
/// deleted Lauf am Nachmittag, turned the treadmill run into a Walk and cropped the marathon.
/// </summary>
public sealed class IncrementalSyncTests
{
    private const string ActivitiesPath = "/api/v3/athlete/activities";
    private const long MorningRun = 20512345678;
    private const long LaufAmNachmittag = 20436100848;
    private const long TreadmillRun = 20312345678;
    private const long Marathon = 20351282630;

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;
    private int _requestsBeforeSecondSync;

    [SetUp]
    public async Task SetUpAsync()
    {
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-1.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-page-2.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        // The marathon's detail is loaded in both syncs; any recorded detailed Activity will do.
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(Marathon), "activity-20479305419.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, DetailPath(Marathon), "activity-20479305419.json");
        _browser = _app.CreateBrowser();
        await _app.ConnectStravaAsync(_browser);
        var firstSync = await TestApp.WaitForSyncAsync(_browser);

        _app.Clock.Advance(TimeSpan.FromHours(1));
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-after-latest-run.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, ActivitiesPath, "activities-empty.json");
        _app.Strava.RespondWithFixture(HttpMethod.Get, FakeStrava.RecentActivitiesPath, "activities-recent-with-edits.json");
        _requestsBeforeSecondSync = _app.Strava.Requests.Count;
        await _browser.PostAsync("/api/sync", null);
        await TestApp.WaitForSyncAsync(_browser, finishedAfter: firstSync.LastSyncedAt);
    }

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task A_new_run_is_imported_by_fetching_only_activities_after_the_latest_run()
    {
        var listRequests = SecondSyncRequests().Where(r => r.Uri.AbsolutePath == ActivitiesPath).ToList();

        (await RunNamesAsync()).ShouldContain("Morning Run");
        // One second before the Evening Run (2026-10-06T18:35:26Z): Strava's "after" is exclusive.
        listRequests[0].Uri.Query.ShouldContain("after=1791311725");
        listRequests.ShouldNotContain(r => r.Uri.Query.Contains("before="));
    }

    [Test]
    public void The_recent_activities_are_checked_with_one_request_for_twenty()
    {
        SecondSyncRequests().Where(r => r.Uri.AbsolutePath == ActivitiesPath && r.Uri.Query.Contains("per_page=20&")).ShouldHaveSingleItem();
    }

    [Test]
    public async Task A_renamed_run_gets_its_new_name()
    {
        var names = await RunNamesAsync();

        names.ShouldContain("Evening Run in the rain");
        names.ShouldNotContain("Evening Run");
    }

    [Test]
    public async Task A_run_changed_to_a_walk_is_removed()
    {
        (await RunNamesAsync()).ShouldNotContain("Treadmill intervals");
        (await _browser.GetAsync($"/api/runs/{TreadmillRun}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_run_deleted_on_strava_is_removed()
    {
        (await RunNamesAsync()).ShouldNotContain("Lauf am Nachmittag");
        (await _browser.GetAsync($"/api/runs/{LaufAmNachmittag}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_run_older_than_the_recent_activities_stays_untouched()
    {
        // Wienerwald trail (2026-09-21) is missing from the recent Activities, but older than all of them.
        (await RunNamesAsync()).ShouldContain("Wienerwald trail");
    }

    [Test]
    public void New_runs_and_runs_whose_distance_or_time_changed_get_their_detail_fetched()
    {
        var detailRequests = SecondSyncRequests().Select(r => r.Uri.AbsolutePath).Where(p => p.StartsWith(DetailPath(0)[..^1])).ToList();

        detailRequests.ShouldContain(DetailPath(MorningRun));
        detailRequests.ShouldContain(DetailPath(Marathon));
        detailRequests.Count.ShouldBe(2);
    }

    [Test]
    public async Task Fetching_a_detail_again_replaces_the_best_efforts_and_splits()
    {
        var marathon = await _browser.GetRunAsync(Marathon);

        marathon.Detail.ShouldBe("loaded");
        marathon.BestEfforts.Count.ShouldBe(6);
        marathon.Splits.Select(s => s.Index).ShouldBe([1, 2, 3, 4, 5, 6]);
        marathon.Distance.ShouldBe(42210);
    }

    private static string DetailPath(long activityId) => $"/api/v3/activities/{activityId}";

    private IEnumerable<RecordedRequest> SecondSyncRequests() => _app.Strava.Requests.Skip(_requestsBeforeSecondSync);

    private async Task<List<string>> RunNamesAsync() =>
        [.. (await _browser.ListRunsAsync()).Items.Select(r => r.Name)];
}
