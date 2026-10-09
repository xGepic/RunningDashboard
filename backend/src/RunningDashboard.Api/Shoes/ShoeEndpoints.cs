using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Shoes;

public static class ShoeEndpoints
{
    public static void MapShoeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shoes", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var shoes = await db.Shoes.ToListAsync(cancellationToken);
            var usage = await db.Runs
                .Where(r => r.ShoeId != null)
                .GroupBy(r => r.ShoeId!)
                .Select(g => new { ShoeId = g.Key, Runs = g.Count(), LastRun = g.Max(r => r.StartLocal) })
                .ToDictionaryAsync(u => u.ShoeId, cancellationToken);

            // Active Shoes first; within each group the most recently used first.
            return shoes
                .Select(s =>
                {
                    var used = usage.GetValueOrDefault(s.Id);
                    return new ShoeResponse(s.Id, s.Name, s.Distance, s.Retired, used?.Runs ?? 0, used?.LastRun);
                })
                .OrderBy(s => s.Retired)
                .ThenByDescending(s => s.LastRun ?? DateTime.MinValue)
                .ThenBy(s => s.Name)
                .ToList();
        });
    }

    /// <param name="Distance">Metres, as Strava counts them.</param>
    /// <param name="Runs">Stored Runs logged with this Shoe.</param>
    /// <param name="LastRun">Local start of the latest of those Runs.</param>
    private sealed record ShoeResponse(string Id, string Name, double Distance, bool Retired, int Runs, DateTime? LastRun);
}
