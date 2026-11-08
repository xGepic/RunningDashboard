namespace RunningDashboard.Api.Tests.Harness;

// The JSON of /api/goal-races/{id}/training-plan that more than one test class uses.

/// <param name="Type">"build", "peak" or "taper".</param>
public sealed record PhaseResponse(string Type, int FirstWeeksToRace, int LastWeeksToRace);

/// <param name="PlannedDistance">Metres, or none.</param>
/// <param name="PlannedLongRun">Metres, or none.</param>
/// <param name="RunningDays">In any order; the days left out are rest days.</param>
public sealed record WeekRequest(int WeeksToRace, double? PlannedDistance, double? PlannedLongRun, RunningDayRequest[] RunningDays, bool RecoveryWeek);

/// <param name="Day">"monday" to "sunday".</param>
/// <param name="RunType">"easy", "long", "tempo" or "intervals".</param>
public sealed record RunningDayRequest(string Day, string? RunType);
