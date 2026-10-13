using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Overview;

/// <param name="Distance">Metres.</param>
internal sealed record YearlyGoalRequest(double? Distance);

/// <param name="Distance">Metres.</param>
internal sealed record YearlyGoalResponse(int Year, double Distance)
{
    public static YearlyGoalResponse From(YearlyGoal goal) => new(goal.Year, goal.Distance);
}
