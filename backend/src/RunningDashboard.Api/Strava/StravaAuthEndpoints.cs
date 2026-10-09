using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Sync;

namespace RunningDashboard.Api.Strava;

/// <summary>
/// Strava's OAuth flow: /connect sends the browser to Strava, /callback brings it back with a code.
/// </summary>
public static class StravaAuthEndpoints
{
    private const string AuthorizeUrl = "https://www.strava.com/oauth/authorize";
    private const string StateCookie = "strava_oauth_state";
    private const string StateCookiePath = "/api/strava";

    private static readonly string[] RequiredScopes = ["read", "activity:read_all", "profile:read_all"];

    public static void MapStravaAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/strava/connect", (HttpContext http, IOptions<StravaOptions> options) =>
        {
            var state = RandomNumberGenerator.GetHexString(32, lowercase: true);
            http.Response.Cookies.Append(StateCookie, state, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Path = StateCookiePath,
                MaxAge = TimeSpan.FromMinutes(10),
            });

            var url = QueryHelpers.AddQueryString(AuthorizeUrl, new Dictionary<string, string?>
            {
                ["client_id"] = options.Value.ClientId,
                ["redirect_uri"] = options.Value.RedirectUri,
                ["response_type"] = "code",
                ["approval_prompt"] = "auto",
                ["scope"] = string.Join(',', RequiredScopes),
                ["state"] = state,
            });
            return Results.Redirect(url);
        });

        app.MapGet("/api/strava/callback", async (
            string? state,
            string? code,
            string? scope,
            string? error,
            HttpContext http,
            StravaClient strava,
            AppDbContext db,
            TimeProvider clock,
            ILoggerFactory loggers,
            SyncWorker sync,
            CancellationToken cancellationToken) =>
        {
            var expectedState = http.Request.Cookies[StateCookie];
            http.Response.Cookies.Delete(StateCookie, new CookieOptions { Path = StateCookiePath });

            if (!StatesMatch(expectedState, state))
                return RedirectWithError(ConnectError.InvalidState);
            if (error is not null || string.IsNullOrEmpty(code))
                return RedirectWithError(ConnectError.AccessDenied);
            if (!HasRequiredScopes(scope))
                return RedirectWithError(ConnectError.MissingPermissions);

            StravaTokenGrant grant;
            try
            {
                grant = await strava.ExchangeCodeAsync(code, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                loggers.CreateLogger(typeof(StravaAuthEndpoints)).LogWarning(exception, "Strava did not accept the authorization code");
                return RedirectWithError(ConnectError.StravaRejected);
            }

            var connection = await db.StravaConnections.SingleOrDefaultAsync(cancellationToken);
            if (connection is null)
                db.StravaConnections.Add(StravaConnection.Create(grant, clock.GetUtcNow()));
            else if (connection.AthleteId != grant.Athlete.Id)
                return RedirectWithError(ConnectError.DifferentAthlete);
            else
                connection.Reconnect(grant);

            (await db.GetSyncStateAsync(cancellationToken)).Reconnected();
            await db.SaveChangesAsync(cancellationToken);
            sync.RequestSyncAfterReconnect();
            return Results.Redirect("/?strava=connected");
        });
    }

    private static bool StatesMatch(string? expected, string? actual) =>
        !string.IsNullOrEmpty(expected)
        && actual is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    /// <summary>
    /// Strava lets the Athlete untick permissions on its consent screen; the granted ones come back in "scope".
    /// </summary>
    private static bool HasRequiredScopes(string? granted)
    {
        var grantedScopes = (granted ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return RequiredScopes.All(grantedScopes.Contains);
    }

    private static IResult RedirectWithError(string reason) => Results.Redirect($"/?strava=error&reason={reason}");

    /// <summary>
    /// Reasons sent to the frontend in ?strava=error&amp;reason=…; the frontend's ConnectErrorReason must list the same values.
    /// </summary>
    private static class ConnectError
    {
        public const string InvalidState = "invalid-state";
        public const string AccessDenied = "access-denied";
        public const string MissingPermissions = "missing-permissions";
        public const string StravaRejected = "strava-rejected";
        public const string DifferentAthlete = "different-athlete";
    }
}
