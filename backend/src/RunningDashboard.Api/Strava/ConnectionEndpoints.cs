using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Strava;

public static class ConnectionEndpoints
{
    public static void MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/connection", async (AppDbContext db, StravaAppCredentials stravaApp, CancellationToken cancellationToken) =>
        {
            var connection = await db.StravaConnections.SingleOrDefaultAsync(cancellationToken);
            var credential = await stravaApp.GetAsync(cancellationToken);
            var app = credential is null ? null : new StravaAppResponse(credential.ClientId, stravaApp.InConfiguration);
            return connection is null
                ? new ConnectionResponse(false, null, null, app)
                : new ConnectionResponse(
                    true,
                    new ConnectedAthlete(connection.AthleteId, connection.FirstName, connection.LastName),
                    connection.ConnectedAt,
                    app);
        });

        // The secret is never sent back; the Settings page asks for both again to change them.
        app.MapPut("/api/strava/app", async (StravaAppRequest request, AppDbContext db, StravaAppCredentials stravaApp, CancellationToken cancellationToken) =>
        {
            var clientId = request.ClientId?.Trim() ?? "";
            var clientSecret = request.ClientSecret?.Trim() ?? "";
            var errors = new Dictionary<string, string[]>();
            if (clientId.Length is 0 or > 20 || !clientId.All(char.IsAsciiDigit))
                errors["clientId"] = ["The Client ID is the number Strava shows for the API app."];
            if (clientSecret.Length is 0 or > 100)
                errors["clientSecret"] = ["Copy the Client Secret from the API app on Strava."];
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var stored = await db.StravaApps.SingleOrDefaultAsync(cancellationToken);
            if (stored is null)
                db.StravaApps.Add(new StravaApp { ClientId = clientId, ClientSecret = clientSecret });
            else
            {
                stored.ClientId = clientId;
                stored.ClientSecret = clientSecret;
            }

            await db.SaveChangesAsync(cancellationToken);
            var credential = (await stravaApp.GetAsync(cancellationToken))!;
            return Results.Ok(new StravaAppResponse(credential.ClientId, stravaApp.InConfiguration));
        });
    }
}
