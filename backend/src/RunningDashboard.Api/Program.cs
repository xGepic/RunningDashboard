using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Backup;
using RunningDashboard.Api.GoalRaces;
using RunningDashboard.Api.Overview;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Records;
using RunningDashboard.Api.Rides;
using RunningDashboard.Api.Runs;
using RunningDashboard.Api.Settings;
using RunningDashboard.Api.Shoes;
using RunningDashboard.Api.Strava;
using RunningDashboard.Api.Swims;
using RunningDashboard.Api.Sync;
using RunningDashboard.Api.Training;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("RunningDashboard")));
builder.Services.AddOptions<StravaOptions>()
    .Bind(builder.Configuration.GetSection(StravaOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<StravaClient>();
builder.Services.AddSingleton<StravaRateLimits>();
builder.Services.AddScoped<ActivityImport>();
builder.Services.AddScoped<RunDetailImport>();
builder.Services.AddScoped<ShoeImport>();
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
app.MapGoalRaceEndpoints();
app.MapTrainingPlanEndpoints();
app.MapTrainingEndpoints();

app.Run();
