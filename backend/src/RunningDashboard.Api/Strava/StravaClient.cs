using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Strava;

/// <summary>
/// The only place that talks to strava.com. API calls use the stored access token, refreshing it first when it
/// is about to expire; callers never deal with tokens.
/// </summary>
public sealed class StravaClient(
    HttpClient http,
    IOptions<StravaOptions> options,
    AppDbContext db,
    TimeProvider clock,
    StravaRateLimits rateLimits)
{
    private static readonly Uri TokenUrl = new("https://www.strava.com/oauth/token");
    private const string ActivitiesUrl = "https://www.strava.com/api/v3/athlete/activities";
    private const string ActivityUrl = "https://www.strava.com/api/v3/activities/";
    private const string AthleteUrl = "https://www.strava.com/api/v3/athlete";

    /// <summary>Refresh this long before Strava's expiry, so a token never runs out mid-request.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions SnakeCaseJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Trades the code from Strava's OAuth callback for tokens and the athlete they belong to.
    /// </summary>
    public async Task<StravaTokenGrant> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var token = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["code"] = code,
            ["grant_type"] = "authorization_code",
        }, cancellationToken);

        return new StravaTokenGrant(
            new StravaAthlete(token.Athlete!.Id, token.Athlete.Firstname, token.Athlete.Lastname),
            token.AccessToken,
            token.RefreshToken,
            DateTimeOffset.FromUnixTimeSeconds(token.ExpiresAt));
    }

    /// <summary>
    /// One page of the athlete's Activities of any sport. Strava lists them newest first, except with only
    /// <see cref="ActivityQuery.After"/> (oldest first). An empty page means there are no more.
    /// </summary>
    public async Task<IReadOnlyList<StravaActivity>> ListActivitiesAsync(ActivityQuery query, CancellationToken cancellationToken)
    {
        var url = $"{ActivitiesUrl}?per_page={query.PerPage}&page={query.Page}";
        if (query.Before is { } before)
            url += $"&before={new DateTimeOffset(before, TimeSpan.Zero).ToUnixTimeSeconds()}";
        if (query.After is { } after)
            url += $"&after={new DateTimeOffset(after, TimeSpan.Zero).ToUnixTimeSeconds()}";
        using var response = await SendApiRequestAsync(url, cancellationToken);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return [.. json.RootElement.EnumerateArray().Select(element =>
        {
            var activity = element.Deserialize<ActivityResponse>(SnakeCaseJson)!;
            return new StravaActivity(
                activity.Id,
                activity.Name,
                activity.SportType,
                activity.StartDate.UtcDateTime,
                // Strava marks start_date_local with "Z" although it is the local wall-clock time.
                activity.StartDateLocal.DateTime,
                activity.Distance,
                activity.MovingTime,
                activity.ElapsedTime,
                activity.TotalElevationGain,
                activity.SufferScore,
                activity.GearId,
                element.GetRawText());
        })];
    }

    /// <summary>
    /// The Athlete's Shoes as Strava lists them on the authenticated athlete, retired ones included; null if the
    /// response has no Shoes at all (not the same as an empty list).
    /// </summary>
    public async Task<IReadOnlyList<StravaShoe>?> ListShoesAsync(CancellationToken cancellationToken)
    {
        using var response = await SendApiRequestAsync(AthleteUrl, cancellationToken);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("shoes", out var shoes) || shoes.ValueKind != JsonValueKind.Array)
            return null;

        return [.. shoes.EnumerateArray().Select(element =>
        {
            var shoe = element.Deserialize<ShoeResponse>(SnakeCaseJson)!;
            return new StravaShoe(shoe.Id, shoe.Name, shoe.Distance, shoe.Retired, element.GetRawText());
        })];
    }

    /// <summary>
    /// The detailed Activity with its Best Efforts and metric splits; null if Strava no longer has it.
    /// </summary>
    public async Task<StravaActivityDetail?> GetActivityDetailAsync(long activityId, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await SendApiRequestAsync($"{ActivityUrl}{activityId}", cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        using (response)
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = JsonSerializer.Deserialize<ActivityDetailResponse>(json, SnakeCaseJson)!;
            return new StravaActivityDetail(
                detail.Id,
                [.. (detail.BestEfforts ?? []).Select(e => new StravaBestEffort(e.Name, e.Distance, e.ElapsedTime))],
                [.. (detail.SplitsMetric ?? []).Select(s => new StravaSplit(s.Split, s.Distance, s.MovingTime, s.ElapsedTime, s.ElevationDifference ?? 0))],
                json);
        }
    }

    /// <summary>
    /// GETs from the API within Strava's rate limits; a 429 waits for the next window and tries again. A refused
    /// access token is refreshed once and tried again; refused again, only reconnecting helps.
    /// </summary>
    private async Task<HttpResponseMessage> SendApiRequestAsync(string url, CancellationToken cancellationToken)
    {
        var forceRefresh = false;
        while (true)
        {
            await rateLimits.WaitForCapacityAsync(cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(forceRefresh, cancellationToken));
            var response = await http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimits.ObserveRefusal(response.Headers);
                response.Dispose();
                continue;
            }

            rateLimits.Observe(response.Headers);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                if (forceRefresh)
                    throw new StravaReconnectRequiredException("Strava refused even a freshly refreshed access token.");
                forceRefresh = true;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode;
                response.Dispose();
                throw new HttpRequestException($"Strava answered {(int)status} ({status}) for {url}.", null, status);
            }

            return response;
        }
    }

    private async Task<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var connection = await db.StravaConnections.SingleOrDefaultAsync(cancellationToken)
            ?? throw new StravaReconnectRequiredException("Strava is not connected.");
        if (!forceRefresh && connection.AccessTokenExpiresAt - RefreshMargin > clock.GetUtcNow())
            return connection.AccessToken;

        TokenResponse token;
        try
        {
            token = await RequestTokenAsync(new Dictionary<string, string>
            {
                ["refresh_token"] = connection.RefreshToken,
                ["grant_type"] = "refresh_token",
            }, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            throw new StravaReconnectRequiredException("Strava refused to refresh the access token.", exception);
        }

        connection.UseTokens(token.AccessToken, token.RefreshToken, DateTimeOffset.FromUnixTimeSeconds(token.ExpiresAt));
        await db.SaveChangesAsync(cancellationToken);
        return connection.AccessToken;
    }

    /// <summary>
    /// POSTs to Strava's token endpoint with the app's credentials plus <paramref name="grant"/>.
    /// </summary>
    private async Task<TokenResponse> RequestTokenAsync(Dictionary<string, string> grant, CancellationToken cancellationToken)
    {
        grant["client_id"] = options.Value.ClientId;
        grant["client_secret"] = options.Value.ClientSecret;
        using var response = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(grant), cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TokenResponse>(SnakeCaseJson, cancellationToken)
            ?? throw new InvalidOperationException("Strava returned an empty token response.");
    }

    private sealed record ActivityResponse(
        long Id,
        string Name,
        string SportType,
        DateTimeOffset StartDate,
        DateTimeOffset StartDateLocal,
        double Distance,
        int MovingTime,
        int ElapsedTime,
        double TotalElevationGain,
        double? SufferScore,
        string? GearId);

    private sealed record ShoeResponse(string Id, string Name, double Distance, bool Retired);

    private sealed record ActivityDetailResponse(long Id, List<BestEffortResponse>? BestEfforts, List<SplitResponse>? SplitsMetric);

    private sealed record BestEffortResponse(string Name, double Distance, int ElapsedTime);

    private sealed record SplitResponse(int Split, double Distance, int MovingTime, int ElapsedTime, double? ElevationDifference);

    /// <param name="Athlete">Only sent when exchanging an authorization code, not when refreshing.</param>
    private sealed record TokenResponse(string AccessToken, string RefreshToken, long ExpiresAt, AthleteResponse? Athlete);

    private sealed record AthleteResponse(long Id, string Firstname, string Lastname);
}

public sealed record StravaAthlete(long Id, string FirstName, string LastName);

public sealed record StravaTokenGrant(StravaAthlete Athlete, string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Which Activities to list: started strictly before / after the given UTC times (whole seconds), one page of them.
/// </summary>
public sealed record ActivityQuery(DateTime? Before = null, DateTime? After = null, int Page = 1, int PerPage = 200);

/// <summary>
/// An Activity summary as listed by Strava; <see cref="RawJson"/> is the untouched JSON for later re-derivation.
/// </summary>
public sealed record StravaActivity(
    long Id,
    string Name,
    string SportType,
    DateTime StartUtc,
    DateTime StartLocal,
    double Distance,
    int MovingTime,
    int ElapsedTime,
    double ElevationGain,
    double? RelativeEffort,
    string? GearId,
    string RawJson);

/// <summary>
/// A detailed Activity: what the summary lacks. <see cref="RawJson"/> is the untouched JSON.
/// </summary>
public sealed record StravaActivityDetail(long Id, IReadOnlyList<StravaBestEffort> BestEfforts, IReadOnlyList<StravaSplit> Splits, string RawJson);

/// <param name="Name">Strava's name for the distance, e.g. "5K".</param>
public sealed record StravaBestEffort(string Name, double Distance, int ElapsedTime);

/// <param name="Index">1-based.</param>
public sealed record StravaSplit(int Index, double Distance, int MovingTime, int ElapsedTime, double ElevationDifference);

/// <param name="Id">Strava's gear id.</param>
/// <param name="Distance">Metres, as Strava counts them.</param>
public sealed record StravaShoe(string Id, string Name, double Distance, bool Retired, string RawJson);

/// <summary>
/// Strava no longer accepts the app's tokens; only the Athlete connecting again fixes it.
/// </summary>
public sealed class StravaReconnectRequiredException(string message, Exception? inner = null) : Exception(message, inner);
