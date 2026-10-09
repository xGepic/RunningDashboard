using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Runs;
using RunningDashboard.Api.Strava;
using RunningDashboard.Api.Sync;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("RunningDashboard")));
builder.Services.AddOptions<StravaOptions>()
    .Bind(builder.Configuration.GetSection(StravaOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<StravaClient>();
builder.Services.AddScoped<RunImport>();
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

app.Run();

public partial class Program;
