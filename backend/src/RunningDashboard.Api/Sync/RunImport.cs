using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Sync;

/// <summary>
/// Pages through all of the Athlete's Activities on Strava and stores the Runs among them.
/// </summary>
public sealed class RunImport(AppDbContext db, StravaClient strava)
{
    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        var connection = await db.StravaConnections.SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Strava is not connected.");

        for (var page = 1; ; page++)
        {
            var activities = await strava.ListActivitiesAsync(connection.AccessToken, page, cancellationToken);
            if (activities.Count == 0)
                return;

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

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
