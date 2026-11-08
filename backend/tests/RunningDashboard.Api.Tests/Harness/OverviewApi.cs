using System.Net.Http.Json;

namespace RunningDashboard.Api.Tests.Harness;

// The JSON of /api/overview, as the tests read it.

public sealed record OverviewResponse(
    TotalsResponse Totals,
    List<YearResponse> Years,
    List<OverviewWeekResponse> Weeks,
    YearlyGoalResponse? YearlyGoal,
    WeeklyTimeGoalResponse? WeeklyTimeGoal,
    StreakResponse? Streak,
    List<TrainingTimeWeekResponse> TrainingTime,
    TrainingTimeWeekResponse? ThisWeek,
    OnThisDayResponse? OnThisDay);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
public sealed record TotalsResponse(int Runs, double Distance, int MovingTime, double ElevationGain);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
public sealed record YearResponse(int Year, int Runs, double Distance, int MovingTime, double ElevationGain);

/// <param name="Start">The Monday.</param>
/// <param name="Distance">Metres.</param>
public sealed record OverviewWeekResponse(DateOnly Start, double Distance);

/// <param name="Distance">Metres.</param>
public sealed record YearlyGoalResponse(int Year, double Distance);

/// <param name="Time">Seconds.</param>
public sealed record WeeklyTimeGoalResponse(int Time);

public sealed record StreakResponse(int Current, DateOnly? Start, bool ThisWeekHasRun, LongestStreakResponse Longest, List<StreakWeekResponse> RecentWeeks);

public sealed record LongestStreakResponse(int Weeks, DateOnly Start, DateOnly End);

public sealed record StreakWeekResponse(DateOnly Start, bool HasRun);

/// <param name="Start">The Monday.</param>
public sealed record TrainingTimeWeekResponse(DateOnly Start, int Runs, int Rides, int Swims);

public sealed record OnThisDayResponse(DateOnly Day, List<OnThisDayRunResponse> Runs);

public sealed record OnThisDayRunResponse(long Id, string Name, DateTime StartLocal, double Distance, int MovingTime);

public static class OverviewApi
{
    public static async Task<OverviewResponse> GetOverviewAsync(this HttpClient browser) =>
        (await browser.GetFromJsonAsync<OverviewResponse>("/api/overview"))!;
}
