using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Sync;

/// <summary>
/// Brings the stored Runs and Rides up to date with the Athlete's Activities on Strava. The first import walks back
/// through the whole history (resumable, see <see cref="SyncState.ImportCursor"/>); later syncs fetch only Activities
/// that started after the newest one seen. Every sync then rechecks the most recent Activities for edits and deletions (ADR 0002).
/// </summary>
public sealed class RunImport(AppDbContext db, StravaClient strava)
{
    /// <summary>How many of the most recent Activities each sync rechecks; one request.</summary>
    private const int RecentActivities = 20;

    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        var state = await db.GetSyncStateAsync(cancellationToken);
        if (state.InitialImportCompleted)
            await FetchNewAsync(state, cancellationToken);
        else
            await WalkBackAsync(state, cancellationToken);

        await RecheckRecentAsync(state, cancellationToken);
    }

    /// <summary>
    /// Newest first, one page at a time; each page is saved together with the cursor, so an interrupted walk
    /// continues where it stopped.
    /// </summary>
    private async Task WalkBackAsync(SyncState state, CancellationToken cancellationToken)
    {
        while (true)
        {
            var activities = await strava.ListActivitiesAsync(new ActivityQuery(Before: state.ImportCursor), cancellationToken);
            if (activities.Count == 0)
            {
                state.ImportCursor = null;
                state.InitialImportCompleted = true;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            await StoreAsync(activities, state, cancellationToken);
            state.ImportCursor = NextCursor(activities, state.ImportCursor);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Pages through the Activities that started after the newest one seen so far (usually one short page).
    /// </summary>
    private async Task FetchNewAsync(SyncState state, CancellationToken cancellationToken)
    {
        // Strava's "after" is exclusive and in whole seconds: one second earlier also keeps Activities that started
        // in the same second as the newest one seen (which itself is just updated).
        var after = state.LatestActivityStart?.AddSeconds(-1);

        for (var page = 1; ; page++)
        {
            var activities = await strava.ListActivitiesAsync(new ActivityQuery(After: after, Page: page), cancellationToken);
            if (activities.Count == 0)
                return;

            await StoreAsync(activities, state, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Updates the Runs and Rides among the most recent Activities and removes stored ones that, within the time those
    /// Activities span, have changed sport or are no longer on Strava. Older Runs and Rides are never touched.
    /// </summary>
    private async Task RecheckRecentAsync(SyncState state, CancellationToken cancellationToken)
    {
        var recent = await strava.ListActivitiesAsync(new ActivityQuery(PerPage: RecentActivities), cancellationToken);
        if (recent.Count == 0)
            return;

        var (stillRuns, stillRides) = await StoreAsync(recent, state, cancellationToken);
        var listed = recent.Select(a => a.Id).ToList();

        // A Run listed with another sport is gone. One missing from the list is gone only if it started strictly after
        // the oldest recent Activity: one in that same second may simply be the 21st, unlisted. The same goes for Rides.
        var windowStart = recent.Min(a => a.StartUtc);
        var noLongerRuns = await db.Runs
            .Where(r => !stillRuns.Contains(r.Id) && (listed.Contains(r.Id) || r.StartUtc > windowStart))
            .ToListAsync(cancellationToken);
        db.Runs.RemoveRange(noLongerRuns);
        var noLongerRides = await db.Rides
            .Where(r => !stillRides.Contains(r.Id) && (listed.Contains(r.Id) || r.StartUtc > windowStart))
            .ToListAsync(cancellationToken);
        db.Rides.RemoveRange(noLongerRides);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Strava's "before" is exclusive and in whole seconds, so continue one second after the oldest Activity: an
    /// Activity in that same second that missed the page is not skipped (the ones already stored are just updated).
    /// Only if that would not move back at all, continue strictly before it.
    /// </summary>
    private static DateTime NextCursor(IReadOnlyList<StravaActivity> page, DateTime? current)
    {
        var oldest = page.Min(a => a.StartUtc);
        var next = oldest.AddSeconds(1);
        return current is { } cursor && next >= cursor ? oldest : next;
    }

    /// <summary>
    /// Adds or updates the Runs and Rides among <paramref name="activities"/> and remembers the newest start seen.
    /// </summary>
    /// <returns>The ids of the Runs and of the Rides among them.</returns>
    private async Task<(List<long> Runs, List<long> Rides)> StoreAsync(IReadOnlyList<StravaActivity> activities, SyncState state, CancellationToken cancellationToken)
    {
        var newest = activities.Max(a => a.StartUtc);
        if (state.LatestActivityStart is null || newest > state.LatestActivityStart)
            state.LatestActivityStart = newest;

        var runs = activities.Where(a => Run.IsRunningSportType(a.SportType)).ToList();
        var ids = runs.Select(a => a.Id).ToList();
        var stored = await db.Runs.Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);

        foreach (var activity in runs)
        {
            if (stored.TryGetValue(activity.Id, out var run))
                run.UpdateFrom(activity);
            else
                db.Runs.Add(Run.From(activity));
        }

        var rides = activities.Where(a => Ride.IsCyclingSportType(a.SportType)).ToList();
        var rideIds = rides.Select(a => a.Id).ToList();
        var storedRides = await db.Rides.Where(r => rideIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);
        foreach (var activity in rides)
        {
            if (storedRides.TryGetValue(activity.Id, out var ride))
                ride.UpdateFrom(activity);
            else
                db.Rides.Add(Ride.From(activity));
        }

        return (ids, rideIds);
    }
}
