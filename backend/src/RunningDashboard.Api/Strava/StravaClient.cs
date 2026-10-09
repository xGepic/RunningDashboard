using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RunningDashboard.Api.Strava;

/// <summary>
/// The only place that talks to strava.com.
/// </summary>
public sealed class StravaClient(HttpClient http, IOptions<StravaOptions> options)
{
    private static readonly Uri TokenUrl = new("https://www.strava.com/oauth/token");
    private const string ActivitiesUrl = "https://www.strava.com/api/v3/athlete/activities";
    private const int ActivitiesPerPage = 200;

    private static readonly JsonSerializerOptions SnakeCaseJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Trades the code from Strava's OAuth callback for tokens and the athlete they belong to.
    /// </summary>
    public async Task<StravaTokenGrant> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = options.Value.ClientId,
            ["client_secret"] = options.Value.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
        }), cancellationToken);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(SnakeCaseJson, cancellationToken)
            ?? throw new InvalidOperationException("Strava returned an empty token response.");

        return new StravaTokenGrant(
            new StravaAthlete(token.Athlete.Id, token.Athlete.Firstname, token.Athlete.Lastname),
            token.AccessToken,
            token.RefreshToken,
            DateTimeOffset.FromUnixTimeSeconds(token.ExpiresAt));
    }

    /// <summary>
    /// One page of the athlete's Activities of any sport, newest first. An empty page means there are no more.
    /// </summary>
    public async Task<IReadOnlyList<StravaActivity>> ListActivitiesAsync(string accessToken, int page, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ActivitiesUrl}?page={page}&per_page={ActivitiesPerPage}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return json.RootElement.EnumerateArray().Select(element =>
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
        }).ToList();
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

    private sealed record TokenResponse(string AccessToken, string RefreshToken, long ExpiresAt, AthleteResponse Athlete);

    private sealed record AthleteResponse(long Id, string Firstname, string Lastname);
}

public sealed record StravaAthlete(long Id, string FirstName, string LastName);

public sealed record StravaTokenGrant(StravaAthlete Athlete, string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

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
