using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Sync;

public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sync", (SyncWorker sync, AppDbContext db, CancellationToken cancellationToken) =>
            sync.GetStatusAsync(db, cancellationToken));

        app.MapPost("/api/sync", async (SyncWorker sync, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (!await db.IsStravaConnectedAsync(cancellationToken))
                return Results.Problem("Connect Strava before syncing.", statusCode: StatusCodes.Status409Conflict);

            sync.RequestSync();
            return Results.Accepted("/api/sync", await sync.GetStatusAsync(db, cancellationToken));
        });
    }
}
