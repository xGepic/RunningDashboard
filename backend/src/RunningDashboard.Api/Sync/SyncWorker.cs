using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Sync;

/// <summary>
/// Runs syncs in the background, one at a time. Asking for a sync while one runs just reports the running one.
/// A sync starts when the backend starts (if Strava is connected), after connecting, and on "Sync now".
/// </summary>
public sealed class SyncWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<SyncWorker> logger) : BackgroundService
{
    private readonly SemaphoreSlim _syncRequested = new(0, 1);
    private readonly Lock _lock = new();
    private bool _running;
    private DateTimeOffset? _lastSyncedAt;

    public SyncStatus Status
    {
        get
        {
            lock (_lock)
                return CurrentStatus();
        }
    }

    public SyncStatus RequestSync()
    {
        lock (_lock)
        {
            if (!_running)
            {
                _running = true;
                _syncRequested.Release();
            }

            return CurrentStatus();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequestSyncIfConnectedAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _syncRequested.WaitAsync(stoppingToken);
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<RunImport>().ImportAsync(stoppingToken);
                lock (_lock)
                    _lastSyncedAt = clock.GetUtcNow();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Sync failed");
            }
            finally
            {
                lock (_lock)
                    _running = false;
            }
        }
    }

    /// <remarks>Never throws: a failure here must not stop the backend.</remarks>
    private async Task RequestSyncIfConnectedAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            if (await scope.ServiceProvider.GetRequiredService<AppDbContext>().IsStravaConnectedAsync(cancellationToken))
                RequestSync();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Could not check the Strava connection at startup; no startup sync");
        }
    }

    /// <remarks>Caller holds <see cref="_lock"/>.</remarks>
    private SyncStatus CurrentStatus() => new(_running, _lastSyncedAt);
}

public sealed record SyncStatus(bool Running, DateTimeOffset? LastSyncedAt);
