using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Records;

public static class RecordEndpoints
{
    /// <summary>
    /// 5k, half marathon and marathon, in metres as Strava has them (its half marathon is 21,097 m, not 21,097.5); the
    /// other distances can be picked on the page.
    /// </summary>
    private static readonly HashSet<double> ShownByDefault = [5000, 21097, 42195];

    public static void MapRecordEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/records", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var efforts = await PersonalBests.BestEfforts(db).ToListAsync(cancellationToken);
            return efforts
                .GroupBy(e => e.Distance)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var progression = PersonalBests.Progression(g).Select(PersonalBestResponse.From).ToList();
                    var name = g.MaxBy(e => e.StartUtc)!.Name;
                    return new RecordDistanceResponse(name, g.Key, ShownByDefault.Contains(g.Key), progression, progression[^1]);
                })
                .ToList();
        });
    }
}
