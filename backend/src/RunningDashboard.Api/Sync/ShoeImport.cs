using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Sync;

/// <summary>
/// Makes the stored Shoes match the ones Strava lists for the Athlete. Strava leaves retired and deleted Shoes out of
/// that list, so a stored Shoe it no longer lists is retired here and kept with its last distance.
/// </summary>
public sealed class ShoeImport(AppDbContext db, StravaClient strava)
{
    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        // Without a Shoe list (e.g. a summary athlete), keep what is stored rather than read it as "all retired".
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

        // Retired (or deleted) on Strava; kept so that their Runs still show their name.
        foreach (var shoe in stored.Values)
            shoe.Retired = true;

        await db.SaveChangesAsync(cancellationToken);
    }
}
