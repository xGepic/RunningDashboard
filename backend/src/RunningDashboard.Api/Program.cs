using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Backup;
using RunningDashboard.Api.Fitness;
using RunningDashboard.Api.GoalRaces;
using RunningDashboard.Api.Overview;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Records;
using RunningDashboard.Api.Review;
using RunningDashboard.Api.Rides;
using RunningDashboard.Api.Runs;
using RunningDashboard.Api.Settings;
using RunningDashboard.Api.Shoes;
using RunningDashboard.Api.Strava;
using RunningDashboard.Api.Swims;
using RunningDashboard.Api.Sync;
using RunningDashboard.Api.Training;
using RunningDashboard.Api.Weather;

// The packaged app (see publish.ps1) brings the built frontend along in wwwroot and keeps its database next to the exe,
// wherever it is started from.
var packaged = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
if (packaged)
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);
if (packaged && builder.Configuration["urls"] is null)
    builder.WebHost.UseUrls("http://localhost:5148");

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("RunningDashboard")));
builder.Services.AddOptions<StravaOptions>().Bind(builder.Configuration.GetSection(StravaOptions.Section));
builder.Services.AddScoped<StravaAppCredentials>();
builder.Services.AddHttpClient<StravaClient>();
builder.Services.AddSingleton<StravaRateLimits>();
// A Run's page waits for the temperature, so not for long.
builder.Services.AddHttpClient<WeatherClient>(client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddScoped<ActivityImport>();
builder.Services.AddScoped<RunDetailImport>();
builder.Services.AddScoped<ShoeImport>();
builder.Services.AddOptions<SyncOptions>().Bind(builder.Configuration.GetSection(SyncOptions.Section));
builder.Services.AddSingleton<SyncWorker>();
builder.Services.AddHostedService(services => services.GetRequiredService<SyncWorker>());
builder.Services.AddOptions<BackupOptions>().Bind(builder.Configuration.GetSection(BackupOptions.Section));
builder.Services.AddSingleton<DatabaseBackup>();
builder.Services.AddHostedService<BackupWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
    // A migration that goes wrong must not take the data with it; a new, empty database has nothing to keep.
    if (database.GetAppliedMigrations().Any() && database.GetPendingMigrations().Any())
        app.Services.GetRequiredService<DatabaseBackup>().BackUp(beforeMigration: true);
    database.Migrate();
}

app.MapConnectionEndpoints();
app.MapStravaAuthEndpoints();
app.MapSyncEndpoints();
app.MapRunEndpoints();
app.MapRideEndpoints();
app.MapSwimEndpoints();
app.MapOverviewEndpoints();
app.MapYearlyGoalEndpoints();
app.MapSettingsEndpoints();
app.MapBackupEndpoints();
app.MapShoeEndpoints();
app.MapRecordEndpoints();
app.MapFitnessEndpoints();
app.MapReviewEndpoints();
app.MapGoalRaceEndpoints();
app.MapTrainingPlanEndpoints();
app.MapTrainingEndpoints();

if (packaged)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    // Angular's routes, e.g. a Run's page opened from a bookmark; files and unknown API routes stay a 404.
    app.MapFallbackToFile("{*path:nonfile:regex(^(?!api/).*$)}", "index.html");
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = app.Urls.First();
        LogOpenAt(app.Logger, url);
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    });
}

app.Run();

// Public, for the tests' WebApplicationFactory<Program>.
public partial class Program
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Running Dashboard is open at {Url}. Close this window to stop it.")]
    private static partial void LogOpenAt(ILogger logger, string url);
}
