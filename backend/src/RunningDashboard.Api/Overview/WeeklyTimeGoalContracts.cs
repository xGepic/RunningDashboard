using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Overview;

/// <param name="Time">Seconds.</param>
internal sealed record WeeklyTimeGoalRequest(int? Time);

/// <param name="Time">Seconds.</param>
internal sealed record WeeklyTimeGoalResponse(int Time)
{
    public static WeeklyTimeGoalResponse From(WeeklyTimeGoal goal) => new(goal.Time);
}
