using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Heatmap;

public static class HeatmapEndpoints
{
    public static void MapHeatmapEndpoints(this IEndpointRouteBuilder app)
    {
        // Every Run's route at once: the summary's simplified one keeps it small (a few hundred KB for hundreds of Runs),
        // and the page switches years without asking again. Treadmill and virtual Runs are left out: a virtual Run's
        // route lies in an invented world.
        app.MapGet("/api/heatmap", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var runs = await db.Runs
                .Where(r => !r.Trainer && r.SportType != Sports.VirtualRun)
                .OrderBy(r => r.StartLocal).ThenBy(r => r.Id)
                .Select(r => new { r.Id, r.Name, r.StartLocal, r.Distance, r.RawSummaryJson })
                .ToListAsync(cancellationToken);

            var withRoutes = runs
                .Select(r => (Summary: r, Route: Run.SummaryRouteIn(r.RawSummaryJson)))
                .Where(r => r.Route is not null)
                .Select(r => new HeatmapRunResponse(r.Summary.Id, r.Summary.Name, r.Summary.StartLocal, r.Summary.Distance, r.Route!))
                .ToList();
            return Results.Ok(new HeatmapResponse(withRoutes));
        });
    }
}
