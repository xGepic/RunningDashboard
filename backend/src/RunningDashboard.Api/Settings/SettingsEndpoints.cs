using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Settings;

/// <summary>Reads and changes the Athlete's settings.</summary>
public static class SettingsEndpoints
{
    /// <summary>Far beyond what any Shoe lasts.</summary>
    private const double MaxShoeMaximumDistance = 5_000_000;

    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/settings", async (AppDbContext db, CancellationToken cancellationToken) =>
            SettingsResponse.From(await db.AthleteSettings.SingleOrDefaultAsync(cancellationToken) ?? new AthleteSettings()));

        // Changes only what the request names, so the Settings page can save one setting without knowing the others.
        app.MapPatch("/api/settings", async (SettingsRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (request.DistanceUnit is not null && !AthleteSettings.IsDistanceUnit(request.DistanceUnit))
                errors["distanceUnit"] = ["The Distance Unit is km or mi."];
            if (request.ShoeMaximumDistance is not null and not (> 0 and <= MaxShoeMaximumDistance))
                errors["shoeMaximumDistance"] = [$"The Maximum Distance is a positive number of metres, at most {MaxShoeMaximumDistance / 1000:0} km."];
            if (request.Theme is not null && !AthleteSettings.IsTheme(request.Theme))
                errors["theme"] = ["The Theme is blue, teal, green, orange, red, pink or purple."];
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var settings = await db.AthleteSettings.SingleOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                settings = new AthleteSettings();
                db.AthleteSettings.Add(settings);
            }

            settings.DistanceUnit = request.DistanceUnit ?? settings.DistanceUnit;
            settings.ShoeMaximumDistance = request.ShoeMaximumDistance ?? settings.ShoeMaximumDistance;
            settings.Theme = request.Theme ?? settings.Theme;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(SettingsResponse.From(settings));
        });
    }
}
