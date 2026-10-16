namespace RunningDashboard.Api.Achievements;

/// <param name="Id">Stable across requests, e.g. "lifetime-1000000".</param>
/// <param name="Group">One of <see cref="AchievementGroup"/>.</param>
/// <param name="Name">How the dashboard names it, e.g. "Half-Marathon" or "1,000 km year".</param>
/// <param name="Distance">The threshold, in metres.</param>
/// <param name="Repeatable">Earned again by every year, month or Training Week that reaches it.</param>
/// <param name="Earned">Each time it was earned, oldest first; empty while it is locked.</param>
/// <param name="Progress">
/// Metres toward it, only for the next locked Achievement of its group: the longest Run, all Runs' distance, or the
/// current year's, month's or Training Week's so far.
/// </param>
internal sealed record AchievementResponse(
    string Id, string Group, string Name, double Distance, bool Repeatable, IReadOnlyList<AchievementEarnedResponse> Earned, double? Progress);

/// <param name="Date">Local start of the Run that earned it.</param>
internal sealed record AchievementEarnedResponse(long RunId, string RunName, DateTime Date);

/// <summary>The groups of Achievements, as the API names them.</summary>
internal static class AchievementGroup
{
    public const string SingleRun = "singleRun";
    public const string Lifetime = "lifetime";
    public const string Year = "year";
    public const string Month = "month";
    public const string Week = "week";
}

/// <param name="StartUtc">When it started, to add Runs up in the order they were run.</param>
/// <param name="StartLocal">Wall-clock start where it was run, which decides its year, month and Training Week.</param>
/// <param name="Distance">Metres.</param>
internal sealed record AchievementRun(long Id, string Name, DateTime StartUtc, DateTime StartLocal, double Distance);
