using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Training;

/// <summary>What the Training page needs to know about one Run.</summary>
/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
public sealed record RunSummary(long Id, string Name, DateTime StartLocal, double Distance, int MovingTime, double? RelativeEffort);

public enum WeekStatus
{
    Past,

    /// <summary>The week today falls in; its actual values are those so far.</summary>
    Current,

    /// <summary>Not started yet, so it has no actual values.</summary>
    Future,
}

/// <summary>One Training Week of a Training Block with what was actually run in it.</summary>
/// <param name="Actual">None for a future week.</param>
public sealed record TrainingBlockWeek(int WeeksToRace, TrainingWeek Week, WeekStatus Status, WeekActual? Actual);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="RelativeEffort">Sum over the week's Runs that have one; none if no Run has.</param>
/// <param name="LongRun">The longest Run of the week; none in a week without Runs.</param>
public sealed record WeekActual(int Runs, double Distance, int MovingTime, double? RelativeEffort, RunSummary? LongRun);

/// <summary>Training Blocks laid out in Training Weeks, with what was actually run.</summary>
public static class TrainingCalendar
{
    /// <summary>The Goal Race whose Training Block <paramref name="date"/> falls in, if any; blocks never overlap.</summary>
    public static GoalRace? RaceTrainingFor(IEnumerable<GoalRace> races, DateOnly date) =>
        races.FirstOrDefault(r => r.TrainingBlock.Contains(date));

    /// <summary>
    /// Every Training Week of <paramref name="block"/>, first to Race Week, with the actual values of
    /// <paramref name="runs"/> (by each Run's local start date) for the weeks that have begun by <paramref name="today"/>.
    /// </summary>
    public static List<TrainingBlockWeek> WeeksOf(TrainingBlock block, IEnumerable<RunSummary> runs, DateOnly today)
    {
        var byWeek = runs.ToLookup(r => TrainingWeek.Of(r.StartLocal));
        var currentWeek = TrainingWeek.Of(today);
        return block.Weeks
            .Select(week =>
            {
                var status = week.Start > currentWeek.Start ? WeekStatus.Future
                    : week == currentWeek ? WeekStatus.Current
                    : WeekStatus.Past;
                var actual = status == WeekStatus.Future ? null : ActualOf(byWeek[week].ToList());
                return new TrainingBlockWeek(block.WeeksToRace(week), week, status, actual);
            })
            .ToList();
    }

    /// <summary>The Long Run: the longest Run; of equally long ones, the one that took longer.</summary>
    public static RunSummary? LongRunOf(IEnumerable<RunSummary> runs) =>
        runs.OrderByDescending(r => r.Distance).ThenByDescending(r => r.MovingTime).ThenBy(r => r.Id).FirstOrDefault();

    /// <summary>
    /// Whole seconds per km by which <paramref name="pace"/> is slower (positive) or faster (negative) than
    /// <paramref name="targetPace"/>, both to the second as the Athlete reads them.
    /// </summary>
    public static int? PaceVsTarget(double? pace, int? targetPace) => pace is null ? null : (int)Math.Round(pace.Value) - targetPace;

    private static WeekActual ActualOf(IReadOnlyCollection<RunSummary> runs)
    {
        var efforts = runs.Where(r => r.RelativeEffort is not null).Select(r => r.RelativeEffort!.Value).ToList();
        return new WeekActual(
            runs.Count,
            runs.Sum(r => r.Distance),
            runs.Sum(r => r.MovingTime),
            efforts.Count > 0 ? efforts.Sum() : null,
            LongRunOf(runs));
    }
}
