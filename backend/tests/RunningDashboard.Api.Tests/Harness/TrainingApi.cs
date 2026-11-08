using System.Net.Http.Json;

namespace RunningDashboard.Api.Tests.Harness;

// The JSON of /api/training, as the tests read it.

/// <param name="View">"block", "countdown" or "none".</param>
public sealed record TrainingResponse(string View, BlockResponse? Block, CountdownResponse? Countdown);

/// <param name="TrainingBlockStart">The Monday the next Training Block starts.</param>
public sealed record CountdownResponse(BlockGoalRaceResponse GoalRace, DateOnly TrainingBlockStart, int DaysRemaining, List<RecentWeekResponse> RecentWeeks);

/// <param name="Start">The Monday.</param>
/// <param name="Distance">Metres.</param>
public sealed record RecentWeekResponse(DateOnly Start, double Distance);

/// <param name="TargetPace">Whole seconds per km.</param>
/// <param name="Phases">None without a Training Plan.</param>
/// <param name="Comparison">The block it is compared with; none when the comparison is off.</param>
public sealed record BlockResponse(
    BlockGoalRaceResponse GoalRace,
    int? TargetPace,
    List<PhaseResponse>? Phases,
    List<BlockWeekResponse> Weeks,
    ComparisonResponse? Comparison);

/// <summary>The Goal Race a Training Block or a countdown is for.</summary>
public sealed record BlockGoalRaceResponse(int Id, string Name, DateOnly Date, double Distance, int? TargetTime);

/// <param name="Weeks">One per Weeks to Race in either block, first to Race Week.</param>
public sealed record ComparisonResponse(BlockGoalRaceResponse GoalRace, List<LinedUpWeekResponse> Weeks);

/// <param name="Viewed">The viewed block's week; none if that block does not have it.</param>
/// <param name="Compared">The compared block's week; none if that block does not have it.</param>
public sealed record LinedUpWeekResponse(int WeeksToRace, BlockWeekResponse? Viewed, BlockWeekResponse? Compared);

/// <summary>One Training Week of a Training Block.</summary>
/// <param name="Status">"past", "current" or "future".</param>
/// <param name="Actual">None for a future week.</param>
/// <param name="Plan">None without a Training Plan.</param>
public sealed record BlockWeekResponse(int WeeksToRace, DateOnly Start, DateOnly End, string Status, ActualResponse? Actual, PlanResponse? Plan);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
public sealed record ActualResponse(int Runs, double Distance, int MovingTime, double? RelativeEffort, LongRunResponse? LongRun);

/// <param name="Distance">Metres.</param>
/// <param name="Pace">Seconds per km.</param>
/// <param name="PaceVsTarget">
/// Whole seconds per km slower (positive) or faster (negative) than the viewed Goal Race's Target Pace.
/// </param>
public sealed record LongRunResponse(long RunId, string Name, DateTime StartLocal, double Distance, double? Pace, int? PaceVsTarget);

/// <param name="PlannedDistance">Metres.</param>
/// <param name="PlannedLongRun">Metres.</param>
/// <param name="DistanceMet">None while the week runs short of it, for a future week, or without a planned distance.</param>
/// <param name="Days">Monday to Sunday.</param>
public sealed record PlanResponse(
    string Phase,
    bool RecoveryWeek,
    double? PlannedDistance,
    double? PlannedLongRun,
    bool? DistanceMet,
    bool? LongRunMet,
    List<DayResponse> Days);

/// <param name="State">"ran", "missed", "unplanned", "planned" (still to come) or "rest".</param>
public sealed record DayResponse(DateOnly Date, string State);

public static class TrainingApi
{
    /// <param name="query">E.g. "goalRaceId=2&amp;compareWith=none", without the "?".</param>
    public static async Task<TrainingResponse> GetTrainingAsync(this HttpClient browser, string query = "") =>
        (await browser.GetFromJsonAsync<TrainingResponse>(query == "" ? "/api/training" : $"/api/training?{query}"))!;
}
