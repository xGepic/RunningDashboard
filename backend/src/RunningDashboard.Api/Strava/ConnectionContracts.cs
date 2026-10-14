namespace RunningDashboard.Api.Strava;

/// <param name="StravaApp">The Strava API app the dashboard connects as; none until one is entered on the Settings page.</param>
internal sealed record ConnectionResponse(bool Connected, ConnectedAthlete? Athlete, DateTimeOffset? ConnectedAt, StravaAppResponse? StravaApp);

internal sealed record ConnectedAthlete(long Id, string FirstName, string LastName);

/// <param name="InConfiguration">Set in the backend's configuration, which wins over the Settings page.</param>
internal sealed record StravaAppResponse(string ClientId, bool InConfiguration);

internal sealed record StravaAppRequest(string? ClientId, string? ClientSecret);
