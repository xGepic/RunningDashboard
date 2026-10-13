namespace RunningDashboard.Api.Strava;

internal sealed record ConnectionResponse(bool Connected, ConnectedAthlete? Athlete, DateTimeOffset? ConnectedAt);

internal sealed record ConnectedAthlete(long Id, string FirstName, string LastName);
