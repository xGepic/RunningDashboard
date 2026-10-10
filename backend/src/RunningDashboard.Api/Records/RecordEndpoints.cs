using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Records;

public static class RecordEndpoints
{
    /// <summary>
    /// 1k, 5k, 10k, half marathon and marathon, in metres as Strava has them (its half marathon is 21,097 m, not 21,097.5);
    /// the other distances can be picked on the page.
    /// </summary>
    private static readonly HashSet<double> ShownByDefault = [1000, 5000, 10000, 21097, 42195];

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

    /// <param name="Name">Strava's name for the distance, e.g. "5K" or "Half-Marathon".</param>
    /// <param name="Distance">Metres.</param>
    /// <param name="ShownByDefault">Whether the Records page shows this distance before the Athlete picks any.</param>
    /// <param name="Progression">Each Best Effort that improved the Personal Best, oldest first.</param>
    private sealed record RecordDistanceResponse(
        string Name,
        double Distance,
        bool ShownByDefault,
        IReadOnlyList<PersonalBestResponse> Progression,
        PersonalBestResponse PersonalBest);

    /// <param name="Date">Local start of the Run.</param>
    /// <param name="ElapsedTime">Seconds.</param>
    /// <param name="TimePerKm">Elapsed time per kilometre, in seconds.</param>
    private sealed record PersonalBestResponse(long RunId, string RunName, DateTime Date, int ElapsedTime, double? TimePerKm)
    {
        public static PersonalBestResponse From(RunBestEffort effort) =>
            new(effort.RunId, effort.RunName, effort.StartLocal, effort.ElapsedTime, Run.PerKilometre(effort.ElapsedTime, effort.Distance));
    }
}
