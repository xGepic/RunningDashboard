using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Strava;

/// <summary>
/// The Strava API app the dashboard talks to Strava as: the one in configuration, otherwise the one entered on the
/// Settings page.
/// </summary>
public sealed class StravaAppCredentials(IOptions<StravaOptions> options, AppDbContext db)
{
    public bool InConfiguration =>
        !string.IsNullOrWhiteSpace(options.Value.ClientId) && !string.IsNullOrWhiteSpace(options.Value.ClientSecret);

    /// <summary>None when neither configuration nor the Settings page has one.</summary>
    public async Task<StravaAppCredential?> GetAsync(CancellationToken cancellationToken)
    {
        if (InConfiguration)
            return new StravaAppCredential(options.Value.ClientId, options.Value.ClientSecret);

        var stored = await db.StravaApps.SingleOrDefaultAsync(cancellationToken);
        return stored is null ? null : new StravaAppCredential(stored.ClientId, stored.ClientSecret);
    }
}

public sealed record StravaAppCredential(string ClientId, string ClientSecret);
