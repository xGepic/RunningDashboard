namespace RunningDashboard.Api.Shoes;

/// <param name="Distance">Metres, as Strava counts them.</param>
/// <param name="Runs">Stored Runs logged with this Shoe.</param>
/// <param name="LastRun">Local start of the latest of those Runs.</param>
/// <param name="RecentDistance">Metres of those Runs in the last <see cref="ShoeEndpoints.RecentWeeks"/> weeks, today included.</param>
internal sealed record ShoeResponse(string Id, string Name, double Distance, bool Retired, int Runs, DateTime? LastRun, double RecentDistance);
