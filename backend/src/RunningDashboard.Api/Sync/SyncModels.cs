namespace RunningDashboard.Api.Sync;

public static class SyncPhase
{
    public const string Idle = "idle";
    /// <summary>Walking through the Activity summaries and storing the Runs, then refreshing the Shoes.</summary>
    public const string ImportingRuns = "importing-runs";

    /// <summary>Fetching the detailed Activity (Best Efforts, splits) of each Run that has none yet.</summary>
    public const string ImportingDetails = "importing-details";
}

/// <param name="Phase">One of <see cref="SyncPhase"/>.</param>
/// <param name="ImportedRuns">Runs stored so far.</param>
/// <param name="DetailedRuns">Runs whose detailed Activity has been fetched.</param>
/// <param name="PausedUntil">Set while the sync waits for Strava's rate limit to reset.</param>
public sealed record SyncStatus(
    bool Running,
    string Phase,
    int ImportedRuns,
    int DetailedRuns,
    DateTimeOffset? PausedUntil,
    DateTimeOffset? LastSyncedAt,
    string? LastError,
    bool ReconnectRequired);
