using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Sync;

/// <summary>
/// Makes the stored Shoes match the ones Strava lists for the Athlete, retired ones included.
/// </summary>
public sealed class ShoeImport(AppDbContext db, StravaClient strava)
{
    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        // Without a Shoe list (e.g. a summary athlete), keep what is stored rather than read it as "all deleted".
        if (await strava.ListShoesAsync(cancellationToken) is not { } listed)
            return;

        var stored = await db.Shoes.ToDictionaryAsync(s => s.Id, cancellationToken);

        foreach (var shoe in listed)
        {
            if (stored.Remove(shoe.Id, out var existing))
                existing.UpdateFrom(shoe);
            else
                db.Shoes.Add(Shoe.From(shoe));
        }

        // Deleted on Strava. Their Runs keep the gear id; they just show no Shoe name any more.
        db.Shoes.RemoveRange(stored.Values);
        await db.SaveChangesAsync(cancellationToken);
    }
}
