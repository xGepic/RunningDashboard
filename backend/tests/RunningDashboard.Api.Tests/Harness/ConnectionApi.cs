using System.Net.Http.Json;

namespace RunningDashboard.Api.Tests.Harness;

// The JSON of /api/connection, as the tests read it.

public sealed record ConnectionResponse(bool Connected, ConnectedAthlete? Athlete, DateTimeOffset? ConnectedAt, StravaAppResponse? StravaApp);

public sealed record ConnectedAthlete(long Id, string FirstName, string LastName);

/// <param name="InConfiguration">Set in the backend's configuration rather than on the Settings page.</param>
public sealed record StravaAppResponse(string ClientId, bool InConfiguration);

public static class ConnectionApi
{
    public static async Task<ConnectionResponse> GetConnectionAsync(this HttpClient browser) =>
        (await browser.GetFromJsonAsync<ConnectionResponse>("/api/connection"))!;
}
