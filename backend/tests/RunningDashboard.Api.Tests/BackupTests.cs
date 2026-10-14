using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// The database is backed up when the backend starts and every 24 hours while it runs, on "Back up now", and before
/// migrations. The 7 most recent regular backups are kept; those made before migrations always.
/// </summary>
public sealed class BackupTests
{
    private readonly TestApp _app = new();

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task The_backend_backs_up_the_database_when_it_starts()
    {
        var browser = _app.CreateBrowser();

        var backup = await WaitForBackupAsync(browser, b => b.LastBackupAt is not null);

        backup.ShouldBe(new BackupResponse(_app.BackupFolder, _app.Clock.GetUtcNow(), null));
        BackupFiles(_app).ShouldBe(["running-dashboard-2026-10-09-180000.db"]);
    }

    [Test]
    public async Task A_restart_within_24_hours_of_the_last_backup_makes_none_a_later_one_does()
    {
        await WaitForBackupAsync(_app.CreateBrowser(), b => b.LastBackupAt is not null);

        await using (var soon = new TestApp(_app.DatabasePath))
        {
            soon.Clock.Advance(TimeSpan.FromHours(23));
            var browser = soon.CreateBrowser();
            await browser.GetAsync("/api/backup");
            await Task.Delay(200);
            (await browser.GetFromJsonAsync<BackupResponse>("/api/backup"))!.LastBackupAt.ShouldBe(_app.Clock.GetUtcNow());
            BackupFiles(_app).Count.ShouldBe(1);
        }

        await using var nextDay = new TestApp(_app.DatabasePath);
        nextDay.Clock.Advance(TimeSpan.FromHours(24));
        await WaitForBackupAsync(nextDay.CreateBrowser(), b => b.LastBackupAt == nextDay.Clock.GetUtcNow());
        BackupFiles(_app).ShouldBe(["running-dashboard-2026-10-09-180000.db", "running-dashboard-2026-10-10-180000.db"]);
    }

    [Test]
    public async Task A_backend_that_keeps_running_backs_up_again_24_hours_later()
    {
        var browser = _app.CreateBrowser();
        await WaitForBackupAsync(browser, b => b.LastBackupAt is not null);

        _app.Clock.Advance(TimeSpan.FromHours(24));

        await WaitForBackupAsync(browser, b => b.LastBackupAt == _app.Clock.GetUtcNow());
    }

    [Test]
    public async Task Back_up_now_backs_up_at_once_keeping_the_seven_most_recent()
    {
        var browser = _app.CreateBrowser();
        await WaitForBackupAsync(browser, b => b.LastBackupAt is not null);

        for (var i = 0; i < 8; i++)
        {
            _app.Clock.Advance(TimeSpan.FromMinutes(1));
            var response = await browser.PostAsync("/api/backup", null);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadFromJsonAsync<BackupResponse>())!.LastBackupAt.ShouldBe(_app.Clock.GetUtcNow());
        }

        BackupFiles(_app).ShouldBe(Enumerable.Range(2, 7).Select(minute => $"running-dashboard-2026-10-09-18{minute:00}00.db"));
    }

    [Test]
    public async Task Pending_migrations_are_applied_only_after_a_backup_that_is_kept_for_good()
    {
        // A database from before the last migration, as after updating the code.
        var path = Path.Combine(Path.GetTempPath(), $"running-dashboard-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        string latest;
        await using (var db = new AppDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261013115737_AddShoeMaximumDistance");
            latest = db.Database.GetMigrations().Last();
        }

        var app = new TestApp(path);
        try
        {
            var browser = app.CreateBrowser();
            await WaitForBackupAsync(browser, b => b.LastBackupAt is not null);
            for (var i = 0; i < 8; i++)
            {
                app.Clock.Advance(TimeSpan.FromMinutes(1));
                await browser.PostAsync("/api/backup", null);
            }

            var beforeMigration = Path.Combine(app.BackupFolder, "running-dashboard-2026-10-09-180000-before-migration.db");
            BackupFiles(app).ShouldContain(Path.GetFileName(beforeMigration));
            BackupFiles(app).Count.ShouldBe(8);
            LastMigrationIn(beforeMigration).ShouldBe("20261013115737_AddShoeMaximumDistance");
            LastMigrationIn(path).ShouldBe(latest);
        }
        finally
        {
            await app.DisposeAsync();
            File.Delete(path);
            Directory.Delete(app.BackupFolder, recursive: true);
        }
    }

    [Test]
    public async Task A_backup_that_cannot_be_written_says_why_until_one_succeeds()
    {
        // A file where the folder should be.
        var blocked = Path.Combine(Path.GetTempPath(), $"running-dashboard-test-{Guid.NewGuid():N}-blocked");
        await File.WriteAllTextAsync(blocked, "");
        var app = new TestApp { BackupFolder = blocked };
        try
        {
            var browser = app.CreateBrowser();

            var response = await browser.PostAsync("/api/backup", null);

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            var backup = await WaitForBackupAsync(browser, b => b.LastError is not null);
            backup.LastBackupAt.ShouldBeNull();
            backup.LastError!.ShouldStartWith($"The backup could not be written to {blocked}: ");

            File.Delete(blocked);
            (await browser.PostAsync("/api/backup", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await browser.GetFromJsonAsync<BackupResponse>("/api/backup"))!.LastError.ShouldBeNull();
        }
        finally
        {
            await app.DisposeAsync();
            File.Delete(blocked);
            if (Directory.Exists(blocked))
                Directory.Delete(blocked, recursive: true);
        }
    }

    private static List<string> BackupFiles(TestApp app) =>
        Directory.GetFiles(app.BackupFolder).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList()!;

    private static string LastMigrationIn(string database)
    {
        using var connection = new SqliteConnection($"Data Source={database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1";
        return (string)command.ExecuteScalar()!;
    }

    /// <summary>Polls until the backup status matches (real time; backups at start run in the background).</summary>
    private static async Task<BackupResponse> WaitForBackupAsync(HttpClient browser, Func<BackupResponse, bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            var backup = (await browser.GetFromJsonAsync<BackupResponse>("/api/backup"))!;
            if (condition(backup))
                return backup;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Backup status never matched: {backup}");
            await Task.Delay(20);
        }
    }

    private sealed record BackupResponse(string Folder, DateTimeOffset? LastBackupAt, string? LastError);
}
