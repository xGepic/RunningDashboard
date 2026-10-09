using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RunningDashboard.Api.Strava;

/// <summary>
/// The only place that talks to strava.com.
/// </summary>
public sealed class StravaClient(HttpClient http, IOptions<StravaOptions> options)
{
    private static readonly Uri TokenUrl = new("https://www.strava.com/oauth/token");

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

    private sealed record TokenResponse(string AccessToken, string RefreshToken, long ExpiresAt, AthleteResponse Athlete);

    private sealed record AthleteResponse(long Id, string Firstname, string Lastname);
}

public sealed record StravaAthlete(long Id, string FirstName, string LastName);

public sealed record StravaTokenGrant(StravaAthlete Athlete, string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
