using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Sync;

/// <summary>
/// Walks back through all of the Athlete's Activities on Strava, newest first, and stores the Runs among them.
/// Each page is saved together with the import cursor, so an interrupted import continues where it stopped.
/// </summary>
public sealed class RunImport(AppDbContext db, StravaClient strava)
{
    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        var state = await db.GetSyncStateAsync(cancellationToken);

        while (true)
        {
            var activities = await strava.ListActivitiesAsync(state.ImportCursor, cancellationToken);
            if (activities.Count == 0)
            {
                state.ImportCursor = null;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            await StoreRunsAsync(activities, cancellationToken);
            state.ImportCursor = NextCursor(activities, state.ImportCursor);
            await db.SaveChangesAsync(cancellationToken);
        }
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

    private async Task StoreRunsAsync(IReadOnlyList<StravaActivity> activities, CancellationToken cancellationToken)
    {
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
    }
}
