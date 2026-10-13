using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Records;

public static class PersonalBests
{
    /// <summary>Every stored Best Effort with its Run; only those over <paramref name="distances"/> (metres), if given.</summary>
    public static IQueryable<RunBestEffort> BestEfforts(AppDbContext db, IReadOnlyCollection<double>? distances = null)
    {
        var efforts = db.Set<BestEffort>().AsQueryable();
        if (distances is not null)
            efforts = efforts.Where(e => distances.Contains(e.Distance));

        return efforts.Join(
            db.Runs,
            e => e.RunId,
            r => r.Id,
            (e, r) => new RunBestEffort(r.Id, r.Name, r.StartUtc, r.StartLocal, e.Name, e.Distance, e.ElapsedTime));
    }

    /// <summary>
    /// How the Personal Best over one distance developed: the Best Efforts in date order, keeping each one strictly
    /// faster than all earlier ones. The last is the current Personal Best.
    /// </summary>
    public static List<RunBestEffort> Progression(IEnumerable<RunBestEffort> efforts)
    {
        var progression = new List<RunBestEffort>();
        foreach (var effort in efforts.OrderBy(e => e.StartUtc).ThenBy(e => e.RunId))
        {
            if (progression.Count == 0 || effort.ElapsedTime < progression[^1].ElapsedTime)
                progression.Add(effort);
        }

        return progression;
    }

    /// <summary>
    /// The distances (metres) at which the Run's Best Effort was a Personal Best when it was run.
    /// </summary>
    public static async Task<HashSet<double>> DistancesSetByAsync(Run run, AppDbContext db, CancellationToken cancellationToken)
    {
        var distances = run.BestEfforts.Select(e => e.Distance).ToList();
        var efforts = await BestEfforts(db, distances).ToListAsync(cancellationToken);

        return [.. efforts
            .GroupBy(e => e.Distance)
            .Where(g => Progression(g).Any(e => e.RunId == run.Id))
            .Select(g => g.Key)];
    }
}
