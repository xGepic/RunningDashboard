namespace RunningDashboard.Api.Records;

/// <summary>A Best Effort together with the Run it is part of.</summary>
/// <param name="Distance">Metres.</param>
/// <param name="ElapsedTime">Seconds.</param>
public sealed record RunBestEffort(long RunId, string RunName, DateTime StartUtc, DateTime StartLocal, string Name, double Distance, int ElapsedTime);
