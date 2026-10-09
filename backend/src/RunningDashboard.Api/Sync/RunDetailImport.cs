using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Sync;

/// <summary>
/// Fetches the detailed Activity of every Run that has none yet, newest first, one Run at a time. Each Run is saved
/// as soon as its detail is in, so an interrupted sync continues with the Runs still missing one. A Run whose detail
/// fails to load is skipped for this sync, so it cannot hold up the older Runs; the next sync tries it again.
/// </summary>
public sealed class RunDetailImport(AppDbContext db, StravaClient strava, TimeProvider clock, ILogger<RunDetailImport> logger)
{
    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        var failed = new List<long>();
        while (await NextRunWithoutDetailAsync(failed, cancellationToken) is { } run)
        {
            StravaActivityDetail? detail;
            try
            {
                detail = await strava.GetActivityDetailAsync(run.Id, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning(exception, "Could not load the detail of Run {RunId}; the next sync tries again", run.Id);
                failed.Add(run.Id);
                continue;
            }

            run.UseDetail(detail, clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            // Each Run carries its raw JSON; don't keep hundreds of them tracked.
            db.ChangeTracker.Clear();
        }

        if (failed.Count > 0)
            throw new RunDetailsMissingException(failed.Count);
    }

    private Task<Run?> NextRunWithoutDetailAsync(List<long> skip, CancellationToken cancellationToken) =>
        db.Runs
            .Include(r => r.BestEfforts)
            .Include(r => r.Splits)
            .Where(r => r.DetailFetchedAt == null && !skip.Contains(r.Id))
            .OrderByDescending(r => r.StartUtc)
            .FirstOrDefaultAsync(cancellationToken);
}

public sealed class RunDetailsMissingException(int runs)
    : Exception($"Could not load the details of {runs} Run{(runs == 1 ? "" : "s")}; the next sync tries again.");
