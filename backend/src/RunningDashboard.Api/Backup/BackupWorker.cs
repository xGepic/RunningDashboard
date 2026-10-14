namespace RunningDashboard.Api.Backup;

/// <summary>
/// Backs up the database when the backend starts and every 24 hours while it runs. A restart within 24 hours of the
/// last backup makes none, so restarting often does not push the older backups out.
/// </summary>
public sealed class BackupWorker(DatabaseBackup backup, TimeProvider clock, ILogger<BackupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval, clock);
        try
        {
            do
                BackUpIfDue();
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private void BackUpIfDue()
    {
        if (!backup.IsDue)
            return;
        try
        {
            backup.BackUp();
        }
        catch (Exception exception)
        {
            // Settings shows it; the next check tries again.
            logger.LogError(exception, "Backup failed");
        }
    }
}
