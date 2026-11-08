namespace RunningDashboard.Api.Heatmap;

/// <summary>A Run on the Heatmap.</summary>
/// <param name="StartLocal">Wall-clock start where the Run happened; its year is the one the Heatmap shows it in.</param>
/// <param name="Distance">Metres.</param>
/// <param name="Route">The summary's simplified route as Strava encodes it (Google's encoded polyline).</param>
internal sealed record HeatmapRunResponse(long Id, string Name, DateTime StartLocal, double Distance, string Route);

/// <param name="Runs">Every Run with a route outdoors, oldest first, so the newest are drawn on top.</param>
internal sealed record HeatmapResponse(IReadOnlyList<HeatmapRunResponse> Runs);
