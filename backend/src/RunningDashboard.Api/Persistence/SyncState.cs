namespace RunningDashboard.Api.Persistence;

/// <summary>
/// What the last syncs left behind, kept across backend restarts. There is at most one row.
/// </summary>
public sealed class SyncState
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>
    /// While an import is unfinished: the start (UTC) of the oldest Activity it has stored. The import continues
    /// with the Activities before it, also after a restart.
    /// </summary>
    public DateTime? ImportCursor { get; set; }

    public string? LastError { get; set; }

    /// <summary>Strava no longer accepts the app's tokens; syncing stays broken until the Athlete reconnects.</summary>
    public bool ReconnectRequired { get; set; }

    /// <summary>A finished sync also proves Strava accepts the app's access again.</summary>
    public void Succeeded(DateTimeOffset now)
    {
        LastSyncedAt = now;
        LastError = null;
        ReconnectRequired = false;
    }

    public void Failed(string error) => LastError = error;

    public void NeedsReconnect(string error)
    {
        ReconnectRequired = true;
        LastError = error;
    }

    public void Reconnected()
    {
        ReconnectRequired = false;
        LastError = null;
    }
}
