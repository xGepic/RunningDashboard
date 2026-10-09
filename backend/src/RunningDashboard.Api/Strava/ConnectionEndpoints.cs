using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Strava;

public static class ConnectionEndpoints
{
    public static void MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/connection", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var connection = await db.StravaConnections.SingleOrDefaultAsync(cancellationToken);
            return connection is null
                ? new ConnectionResponse(false, null, null)
                : new ConnectionResponse(
                    true,
                    new ConnectedAthlete(connection.AthleteId, connection.FirstName, connection.LastName),
                    connection.ConnectedAt);
        });
    }

    private sealed record ConnectionResponse(bool Connected, ConnectedAthlete? Athlete, DateTimeOffset? ConnectedAt);

    private sealed record ConnectedAthlete(long Id, string FirstName, string LastName);
}
