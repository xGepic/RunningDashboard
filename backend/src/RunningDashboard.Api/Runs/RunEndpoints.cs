using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Runs;

public static class RunEndpoints
{
    public static void MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/runs", async (AppDbContext db, CancellationToken cancellationToken) =>
            await db.Runs
                .OrderByDescending(r => r.StartUtc)
                .Select(r => new RunResponse(r.Id, r.Name, r.SportType, r.StartLocal, r.Distance, r.MovingTime, Run.PaceOf(r.MovingTime, r.Distance)))
                .ToListAsync(cancellationToken));
    }

    /// <param name="Distance">Metres.</param>
    /// <param name="MovingTime">Seconds.</param>
    /// <param name="Pace">Seconds per kilometre.</param>
    private sealed record RunResponse(long Id, string Name, string SportType, DateTime StartLocal, double Distance, int MovingTime, double? Pace);
}
