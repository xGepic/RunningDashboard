namespace RunningDashboard.Api.Backup;

/// <param name="Folder">The backup folder, as a full path.</param>
/// <param name="LastBackupAt">When the newest backup was made; null when there is none.</param>
/// <param name="LastError">Why the last backup failed; null once one succeeds.</param>
internal sealed record BackupResponse(string Folder, DateTimeOffset? LastBackupAt, string? LastError)
{
    public static BackupResponse From(DatabaseBackup backup) => new(backup.Folder, backup.LastBackupAt, backup.LastError);
}
