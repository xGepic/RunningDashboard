namespace RunningDashboard.Api.Strava;

/// <param name="StravaApp">The Strava API app the dashboard connects as; none until one is entered on the Settings page.</param>
internal sealed record ConnectionResponse(bool Connected, ConnectedAthlete? Athlete, DateTimeOffset? ConnectedAt, StravaAppResponse? StravaApp);

internal sealed record ConnectedAthlete(long Id, string FirstName, string LastName);

/// <param name="InConfiguration">Set in the backend's configuration, which wins over the Settings page.</param>
internal sealed record StravaAppResponse(string ClientId, bool InConfiguration);

internal sealed record StravaAppRequest(string? ClientId, string? ClientSecret);

/// <param name="ObservedAt">When Strava last reported its usage; none until Strava has answered since the backend started.</param>
/// <param name="Limits">Reads first, each per 15 minutes and then per day; none until Strava has answered.</param>
internal sealed record RateLimitsResponse(DateTimeOffset? ObservedAt, IReadOnlyList<RateLimitResponse> Limits)
{
    public static RateLimitsResponse From(IReadOnlyList<RateLimitUsage> usage) =>
        new(usage.Count == 0 ? null : usage.Max(u => u.ObservedAt), [.. usage.Select(RateLimitResponse.From)]);
}

/// <param name="Requests">"read" counts the read requests, which the syncs send; "all" counts every request.</param>
/// <param name="Period">"fifteen-minutes" (windows start at :00, :15, :30 and :45) or "day" (a UTC day).</param>
/// <param name="ResetsAt">When the count starts again from zero.</param>
internal sealed record RateLimitResponse(string Requests, string Period, int Used, int Limit, DateTimeOffset ResetsAt)
{
    public static RateLimitResponse From(RateLimitUsage usage) =>
        new(usage.ReadsOnly ? "read" : "all", usage.Daily ? "day" : "fifteen-minutes", usage.Used, usage.Limit, usage.ResetsAt);
}
