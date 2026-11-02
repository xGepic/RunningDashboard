using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Shoes;

public static class ShoeEndpoints
{
    /// <summary>How many weeks back a Shoe's recent distance goes, for when it is due to reach the Maximum Distance.</summary>
    internal const int RecentWeeks = 5;

    public static void MapShoeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/shoes", async (AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var shoes = await db.Shoes.ToListAsync(cancellationToken);
            var recentFrom = clock.Today().AddDays(1 - RecentWeeks * 7).ToDateTime(TimeOnly.MinValue);
            var usage = await db.Runs
                .Where(r => r.ShoeId != null)
                .GroupBy(r => r.ShoeId!)
                .Select(g => new
                {
                    ShoeId = g.Key,
                    Runs = g.Count(),
                    LastRun = g.Max(r => r.StartLocal),
                    RecentDistance = g.Where(r => r.StartLocal >= recentFrom).Sum(r => r.Distance),
                })
                .ToDictionaryAsync(u => u.ShoeId, cancellationToken);

            // Active Shoes first; within each group the most recently used first.
            return shoes
                .Select(s =>
                {
                    var used = usage.GetValueOrDefault(s.Id);
                    return new ShoeResponse(s.Id, s.Name, s.Distance, s.Retired, used?.Runs ?? 0, used?.LastRun, used?.RecentDistance ?? 0);
                })
                .OrderBy(s => s.Retired)
                .ThenByDescending(s => s.LastRun ?? DateTime.MinValue)
                .ThenBy(s => s.Name)
                .ToList();
        });
    }
}
