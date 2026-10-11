using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// After the first import, a sync fetches only what is new and rechecks the 20 most recent Activities (ADR 0002).
/// The scenario: since the first import, the Athlete ran a Morning Run and, on Strava, renamed the Evening Run,
/// deleted Lauf am Nachmittag, turned the treadmill run into a Walk and cropped the marathon.
/// </summary>
public sealed class IncrementalSyncTests : IAsyncLifetime
{
    private const string ActivitiesPath = "/api/v3/athlete/activities";
    private const long MorningRun = 20512345678;
    private const long LaufAmNachmittag = 20436100848;
    private const long TreadmillRun = 20312345678;
    private const long Marathon = 20351282630;

    private readonly TestApp _app = new();
    private HttpClient _browser = null!;
    private int _requestsBeforeSecondSync;

    public async Task InitializeAsync()
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

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task A_new_run_is_imported_by_fetching_only_activities_after_the_latest_run()
    {
        var listRequests = SecondSyncRequests().Where(r => r.Uri.AbsolutePath == ActivitiesPath).ToList();

        Assert.Contains("Morning Run", await RunNamesAsync());
        // One second before the Evening Run (2026-10-06T18:35:26Z): Strava's "after" is exclusive.
        Assert.Contains("after=1791311725", listRequests[0].Uri.Query);
        Assert.DoesNotContain(listRequests, r => r.Uri.Query.Contains("before="));
    }

    [Fact]
    public void The_recent_activities_are_checked_with_one_request_for_twenty()
    {
        Assert.Single(SecondSyncRequests(), r => r.Uri.AbsolutePath == ActivitiesPath && r.Uri.Query.Contains("per_page=20&"));
    }

    [Fact]
    public async Task A_renamed_run_gets_its_new_name()
    {
        var names = await RunNamesAsync();

        Assert.Contains("Evening Run in the rain", names);
        Assert.DoesNotContain("Evening Run", names);
    }

    [Fact]
    public async Task A_run_changed_to_a_walk_is_removed()
    {
        Assert.DoesNotContain("Treadmill intervals", await RunNamesAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _browser.GetAsync($"/api/runs/{TreadmillRun}")).StatusCode);
    }

    [Fact]
    public async Task A_run_deleted_on_strava_is_removed()
    {
        Assert.DoesNotContain("Lauf am Nachmittag", await RunNamesAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _browser.GetAsync($"/api/runs/{LaufAmNachmittag}")).StatusCode);
    }

    [Fact]
    public async Task A_run_older_than_the_recent_activities_stays_untouched()
    {
        // Wienerwald trail (2026-09-21) is missing from the recent Activities, but older than all of them.
        Assert.Contains("Wienerwald trail", await RunNamesAsync());
    }

    [Fact]
    public void New_runs_and_runs_whose_distance_or_time_changed_get_their_detail_fetched()
    {
        var detailRequests = SecondSyncRequests().Select(r => r.Uri.AbsolutePath).Where(p => p.StartsWith(DetailPath(0)[..^1])).ToList();

        Assert.Contains(DetailPath(MorningRun), detailRequests);
        Assert.Contains(DetailPath(Marathon), detailRequests);
        Assert.Equal(2, detailRequests.Count);
    }

    [Fact]
    public async Task Fetching_a_detail_again_replaces_the_best_efforts_and_splits()
    {
        var marathon = (await _browser.GetFromJsonAsync<RunDetailResponse>($"/api/runs/{Marathon}"))!;

        Assert.Equal("loaded", marathon.Detail);
        Assert.Equal(6, marathon.BestEfforts.Count);
        Assert.Equal([1, 2, 3, 4, 5, 6], marathon.Splits.Select(s => s.Index));
        Assert.Equal(42210, marathon.Distance);
    }

    private static string DetailPath(long activityId) => $"/api/v3/activities/{activityId}";

    private IEnumerable<RecordedRequest> SecondSyncRequests() => _app.Strava.Requests.Skip(_requestsBeforeSecondSync);

    private async Task<List<string>> RunNamesAsync() =>
        [.. (await _browser.GetFromJsonAsync<RunPage>("/api/runs"))!.Items.Select(r => r.Name)];

    private sealed record RunPage(List<RunResponse> Items);

    private sealed record RunResponse(long Id, string Name);

    private sealed record RunDetailResponse(double Distance, string Detail, List<object> BestEfforts, List<SplitResponse> Splits);

    private sealed record SplitResponse(int Index);
}
