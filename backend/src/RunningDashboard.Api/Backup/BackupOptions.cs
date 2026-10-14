namespace RunningDashboard.Api.Backup;

/// <summary>Where the database's backups go: a setting for the PC, so it lives in appsettings and not in the UI.</summary>
public sealed class BackupOptions
{
    public const string Section = "Backup";

    /// <summary>
    /// The backup folder. A relative path is relative to the database's folder. Pointing it at a synced folder (e.g.
    /// OneDrive) keeps a copy off the PC.
    /// </summary>
    public string Folder { get; set; } = "backups";
}
