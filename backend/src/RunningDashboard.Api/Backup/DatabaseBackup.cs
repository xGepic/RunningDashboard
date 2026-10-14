using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace RunningDashboard.Api.Backup;

/// <summary>
/// Copies the SQLite database into the backup folder with SQLite's backup API, which is safe while the app uses the
/// database. The backups' file names say when they were made, so the last backup is known again after a restart.
/// The <see cref="RegularBackupsKept"/> most recent regular backups are kept; those made before migrations are never
/// removed. A backup holds everything in the database, the Strava refresh token too.
/// </summary>
public sealed class DatabaseBackup
{
    public const int RegularBackupsKept = 7;

    /// <summary>How old the last backup may get before the next is due.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);

    private const string Prefix = "running-dashboard-";
    private const string BeforeMigrationSuffix = "-before-migration";
    private const string Extension = ".db";

    /// <summary>Local time, to the second: sorts by age and reads well in the folder.</summary>
    private const string TimeFormat = "yyyy-MM-dd-HHmmss";

    private readonly string _connectionString;
    private readonly TimeProvider _clock;
    private readonly ILogger<DatabaseBackup> _logger;
    private readonly Lock _lock = new();
    private string? _lastError;

    public DatabaseBackup(IConfiguration configuration, IOptions<BackupOptions> options, TimeProvider clock, ILogger<DatabaseBackup> logger)
    {
        _connectionString = configuration.GetConnectionString("RunningDashboard")!;
        _clock = clock;
        _logger = logger;
        var database = Path.GetFullPath(new SqliteConnectionStringBuilder(_connectionString).DataSource);
        Folder = Path.GetFullPath(options.Value.Folder, Path.GetDirectoryName(database)!);
    }

    /// <summary>The backup folder, as a full path.</summary>
    public string Folder { get; }

    /// <summary>Why the last backup failed; null once one succeeds.</summary>
    public string? LastError
    {
        get
        {
            lock (_lock)
                return _lastError;
        }
    }

    /// <summary>When the newest backup in the folder was made, of either kind; null when there is none.</summary>
    public DateTimeOffset? LastBackupAt => Backups().Select(b => (DateTimeOffset?)b.MadeAt).Max();

    public bool IsDue => LastBackupAt is not { } last || _clock.GetUtcNow() - last >= Interval;

    /// <summary>Backs up now; throws if the backup cannot be written, and keeps why for <see cref="LastError"/>.</summary>
    /// <param name="beforeMigration">Made right before migrations, and so never removed.</param>
    public void BackUp(bool beforeMigration = false)
    {
        lock (_lock)
        {
            try
            {
                var name = Prefix + _clock.GetLocalNow().ToString(TimeFormat, CultureInfo.InvariantCulture)
                    + (beforeMigration ? BeforeMigrationSuffix : "") + Extension;
                Copy(Path.Combine(Folder, name));
                RemoveOldBackups();
                _lastError = null;
                _logger.LogInformation("Backed up the database to {Backup}", name);
            }
            catch (Exception exception)
            {
                _lastError = $"The backup could not be written to {Folder}: {exception.Message}";
                throw;
            }
        }
    }

    /// <summary>Into a partial file first, so a backup that fails halfway is never taken for a whole one.</summary>
    private void Copy(string path)
    {
        Directory.CreateDirectory(Folder);
        var partial = path + ".partial";
        using (var source = new SqliteConnection(_connectionString))
        // Not pooled, so the file is closed again once it is written and can be moved and, later, removed.
        using (var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = partial, Pooling = false }.ToString()))
        {
            // The backup waits for a sync's writes instead of failing on them.
            foreach (var connection in new[] { source, target })
            {
                connection.Open();
                using var wait = connection.CreateCommand();
                wait.CommandText = $"PRAGMA busy_timeout = {(int)LockTimeout.TotalMilliseconds}";
                wait.ExecuteNonQuery();
            }

            source.BackupDatabase(target);
        }

        File.Move(partial, path, overwrite: true);
    }

    private void RemoveOldBackups()
    {
        foreach (var old in Backups().Where(b => !b.BeforeMigration).OrderByDescending(b => b.MadeAt).Skip(RegularBackupsKept))
            File.Delete(old.Path);
    }

    /// <summary>The backups in the folder; files not named like one are left alone.</summary>
    private IEnumerable<BackupFile> Backups()
    {
        if (!Directory.Exists(Folder))
            yield break;

        foreach (var path in Directory.EnumerateFiles(Folder, Prefix + "*" + Extension))
        {
            var time = Path.GetFileNameWithoutExtension(path)[Prefix.Length..];
            var beforeMigration = time.EndsWith(BeforeMigrationSuffix, StringComparison.Ordinal);
            if (beforeMigration)
                time = time[..^BeforeMigrationSuffix.Length];
            if (DateTime.TryParseExact(time, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
                yield return new BackupFile(path, new DateTimeOffset(local, _clock.LocalTimeZone.GetUtcOffset(local)), beforeMigration);
        }
    }

    private sealed record BackupFile(string Path, DateTimeOffset MadeAt, bool BeforeMigration);
}
