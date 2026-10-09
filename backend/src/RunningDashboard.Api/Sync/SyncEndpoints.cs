using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Sync;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sync", (SyncWorker sync) => sync.Status);

        app.MapPost("/api/sync", async (SyncWorker sync, AppDbContext db, CancellationToken cancellationToken) =>
            await db.IsStravaConnectedAsync(cancellationToken)
                ? Results.Accepted("/api/sync", sync.RequestSync())
                : Results.Problem("Connect Strava before syncing.", statusCode: StatusCodes.Status409Conflict));
    }
}
