using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Journey;

public static class JourneyEndpoints
{
    public static void MapJourneyEndpoints(this IEndpointRouteBuilder app)
    {
        // One calendar year on the Virtual Journey, the current year's so far.
        app.MapGet("/api/journey", async (int? year, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            if (YearParameter.Reject(year) is { } rejected)
                return rejected;

            var thisYear = clock.Today().Year;
            var journey = await VirtualJourney.ForYearAsync(db, year ?? thisYear, cancellationToken);
            var years = await db.Runs.Select(r => r.StartLocal.Year).Distinct().ToListAsync(cancellationToken);
            return Results.Ok(new JourneyPageResponse(journey, [.. years.Append(thisYear).Distinct().OrderDescending()]));
        });
    }
}
