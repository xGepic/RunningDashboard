using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Settings;

/// <summary>Reads and changes the Athlete's settings.</summary>
public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/settings", async (AppDbContext db, CancellationToken cancellationToken) =>
            SettingsResponse.From(await db.AthleteSettings.SingleOrDefaultAsync(cancellationToken) ?? new AthleteSettings()));

        app.MapPut("/api/settings", async (SettingsRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (!AthleteSettings.IsDistanceUnit(request.DistanceUnit))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["distanceUnit"] = ["The Distance Unit is km or mi."],
                });

            var settings = await db.AthleteSettings.SingleOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                settings = new AthleteSettings();
                db.AthleteSettings.Add(settings);
            }

            settings.DistanceUnit = request.DistanceUnit!;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(SettingsResponse.From(settings));
        });
    }
}
