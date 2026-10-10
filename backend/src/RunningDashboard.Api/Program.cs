using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.GoalRaces;
using RunningDashboard.Api.Overview;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Records;
using RunningDashboard.Api.Runs;
using RunningDashboard.Api.Shoes;
using RunningDashboard.Api.Strava;
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
builder.Services.AddScoped<RunImport>();
builder.Services.AddScoped<RunDetailImport>();
builder.Services.AddScoped<ShoeImport>();
builder.Services.AddSingleton<SyncWorker>();
builder.Services.AddHostedService(services => services.GetRequiredService<SyncWorker>());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.MapConnectionEndpoints();
app.MapStravaAuthEndpoints();
app.MapSyncEndpoints();
app.MapRunEndpoints();
app.MapOverviewEndpoints();
app.MapShoeEndpoints();
app.MapRecordEndpoints();
app.MapGoalRaceEndpoints();
app.MapTrainingPlanEndpoints();
app.MapTrainingEndpoints();

app.Run();

public partial class Program;
