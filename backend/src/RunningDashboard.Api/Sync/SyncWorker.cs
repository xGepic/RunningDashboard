using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Sync;

/// <summary>
/// Runs syncs in the background, one at a time. Asking for a sync while one runs just reports the running one,
/// except after a reconnect: the running sync may still use the old tokens, so another sync follows it.
/// A sync starts when the backend starts (if Strava is connected), after connecting, on "Sync now", and every
/// <see cref="SyncOptions.Interval"/> while the backend runs.
/// </summary>
public sealed class SyncWorker(
    IServiceScopeFactory scopes,
    StravaRateLimits rateLimits,
    TimeProvider clock,
    IOptions<SyncOptions> options,
    ILogger<SyncWorker> logger) : BackgroundService
{
    private readonly SemaphoreSlim _syncRequested = new(0, 1);
    private readonly Lock _lock = new();
    private bool _running;
    private bool _followUpRequested;
    private string _phase = SyncPhase.Idle;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
                return _running;
        }
    }

    public void RequestSync() => RequestSync(followUpIfRunning: false);

    /// <summary>Syncs with the new tokens, after the running sync if there is one.</summary>
    public void RequestSyncAfterReconnect() => RequestSync(followUpIfRunning: true);

    private void RequestSync(bool followUpIfRunning)
    {
        lock (_lock)
        {
            if (_running)
            {
                _followUpRequested |= followUpIfRunning;
                return;
            }

            _running = true;
            _phase = SyncPhase.ImportingActivities;
            _syncRequested.Release();
        }
    }

    public async Task<SyncStatus> GetStatusAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var state = await db.GetSyncStateAsync(cancellationToken);
        // Both Run counts in one query, so a sync between two queries cannot show more Runs detailed than imported.
        var runs = await db.Runs
            .GroupBy(r => 1)
            .Select(g => new { Imported = g.Count(), Detailed = g.Count(r => r.DetailFetchedAt != null) })
            .SingleOrDefaultAsync(cancellationToken);
        var (importedRuns, detailedRuns) = (runs?.Imported ?? 0, runs?.Detailed ?? 0);
        var importedActivities = importedRuns + await db.ListedActivities.CountAsync(cancellationToken);

        // Read after the counts, so the phase is never older than them.
        bool running;
        string phase;
        lock (_lock)
            (running, phase) = (_running, _running ? _phase : SyncPhase.Idle);
        return new SyncStatus(
            running,
            phase,
            importedActivities,
            importedRuns,
            detailedRuns,
            running ? rateLimits.PausedUntil : null,
            state.LastSyncedAt,
            state.LastError,
            state.ReconnectRequired);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequestSyncIfConnectedAsync(unlessReconnectRequired: false, stoppingToken);
        await Task.WhenAll(SyncWhenRequestedAsync(stoppingToken), SyncPeriodicallyAsync(stoppingToken));
    }

    /// <summary>
    /// Asks for a sync every <see cref="SyncOptions.Interval"/>. One that comes while a sync runs, e.g. a first import
    /// paused for the rate limits, is dropped, as "Sync now" would be.
    /// </summary>
    private async Task SyncPeriodicallyAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.Interval;
        if (interval <= TimeSpan.Zero)
            return;

        using var timer = new PeriodicTimer(interval, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RequestSyncIfConnectedAsync(unlessReconnectRequired: true, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task SyncWhenRequestedAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _syncRequested.WaitAsync(stoppingToken);
                await SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // E.g. the database is locked while recording the outcome; never let it stop the backend.
                logger.LogError(exception, "Sync could not finish");
            }
            finally
            {
                lock (_lock)
                {
                    _running = _followUpRequested;
                    if (_followUpRequested)
                    {
                        _phase = SyncPhase.ImportingActivities;
                        _syncRequested.Release();
                    }
                    _followUpRequested = false;
                }
            }
        }
    }

    private async Task SyncAsync(CancellationToken stoppingToken)
    {
        Action<SyncState> outcome;
        try
        {
            SetPhase(SyncPhase.ImportingActivities);
            using (var scope = scopes.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ActivityImport>().ImportAsync(stoppingToken);

            var shoesRefreshed = await RefreshShoesAsync(stoppingToken);

            SetPhase(SyncPhase.ImportingDetails);
            using (var scope = scopes.CreateScope())
                await scope.ServiceProvider.GetRequiredService<RunDetailImport>().ImportAsync(stoppingToken);

            if (!shoesRefreshed)
                throw new ShoesNotRefreshedException();

            var now = clock.GetUtcNow();
            outcome = state => state.Succeeded(now);
        }
        catch (StravaReconnectRequiredException exception)
        {
            logger.LogWarning(exception, "Sync stopped: Strava needs to be reconnected");
            outcome = state => state.NeedsReconnect("Strava no longer accepts the app's access. Reconnect Strava.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Sync failed");
            outcome = state => state.Failed($"Sync failed: {exception.Message}");
        }

        // A fresh scope: the import's DbContext may hold changes that failed to save.
        using var outcomeScope = scopes.CreateScope();
        var db = outcomeScope.ServiceProvider.GetRequiredService<AppDbContext>();
        outcome(await db.GetSyncStateAsync(stoppingToken));
        await db.SaveChangesAsync(stoppingToken);
    }

    /// <summary>
    /// Refreshes the Shoes; a failure is logged and reported after the Run details, which it must not hold up.
    /// </summary>
    private async Task<bool> RefreshShoesAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ShoeImport>().ImportAsync(stoppingToken);
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException)
        {
            logger.LogWarning(exception, "Could not refresh the Shoes; the next sync tries again");
            return false;
        }
    }

    private void SetPhase(string phase)
    {
        lock (_lock)
            _phase = phase;
    }

    /// <summary>Starts a sync if Strava is connected.</summary>
    /// <param name="unlessReconnectRequired">
    /// Not while Strava waits to be reconnected: a sync nobody asked for would only fail the same way again.
    /// </param>
    /// <remarks>Never throws: a failure here must not stop the backend.</remarks>
    private async Task RequestSyncIfConnectedAsync(bool unlessReconnectRequired, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await db.IsStravaConnectedAsync(cancellationToken))
                return;
            if (unlessReconnectRequired && await db.SyncStates.AnyAsync(s => s.ReconnectRequired, cancellationToken))
                return;
            RequestSync();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Could not check the Strava connection; no sync this time");
        }
    }
}

public sealed class ShoesNotRefreshedException()
    : Exception("Could not refresh the Shoes; the next sync tries again.");
