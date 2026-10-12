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
/// <param name="Plan">What the Training Plan has for the week, against what was run; none without a plan.</param>
public sealed record TrainingBlockWeek(int WeeksToRace, TrainingWeek Week, WeekStatus Status, WeekActual? Actual, WeekPlan? Plan);

/// <summary>How one day of a planned Training Week went.</summary>
public enum DayState
{
    /// <summary>A Running Day with a Run.</summary>
    Ran,

    /// <summary>A Running Day that has passed without a Run.</summary>
    Missed,

    /// <summary>A Run on a day that is not a Running Day.</summary>
    Unplanned,

    /// <summary>A Running Day still to come, today included.</summary>
    Planned,

    /// <summary>Neither a Running Day nor a Run.</summary>
    Rest,
}

/// <summary>One day of a planned Training Week on the calendar strip.</summary>
public sealed record PlanDay(DateOnly Date, DayState State);

/// <summary>A planned Training Week against what was run in it.</summary>
/// <param name="PlannedDistance">Metres; none if not planned.</param>
/// <param name="PlannedLongRun">Metres; none if not planned.</param>
/// <param name="DistanceMet">
/// True once the week's distance reached the planned one, in km to two decimals as the Athlete reads them; false
/// once a week is over without; none while it still runs short, for a future week, or without a planned distance.
/// </param>
/// <param name="LongRunMet">Likewise for the Long Run.</param>
/// <param name="Days">Monday to Sunday.</param>
public sealed record WeekPlan(
    PhaseType Phase,
    bool RecoveryWeek,
    double? PlannedDistance,
    double? PlannedLongRun,
    bool? DistanceMet,
    bool? LongRunMet,
    IReadOnlyList<PlanDay> Days);

/// <summary>One Weeks to Race across two Training Blocks; either block may not have that week.</summary>
public sealed record LinedUpWeek(int WeeksToRace, TrainingBlockWeek? Viewed, TrainingBlockWeek? Compared);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="RelativeEffort">Sum over the week's Runs that have one; none if no Run has.</param>
/// <param name="LongRun">The longest Run of the week; none in a week without Runs.</param>
public sealed record WeekActual(int Runs, double Distance, int MovingTime, double? RelativeEffort, RunSummary? LongRun);

/// <summary>Training Blocks laid out in Training Weeks, with what was actually run.</summary>
public static class TrainingCalendar
{
    /// <summary>How many Training Weeks, up to the current one, count as recent between Training Blocks.</summary>
    public const int RecentWeekCount = 5;

    /// <summary>The Goal Race whose Training Block <paramref name="date"/> falls in, if any; blocks never overlap.</summary>
    public static GoalRace? RaceTrainingFor(IEnumerable<GoalRace> races, DateOnly date) =>
        races.FirstOrDefault(r => r.TrainingBlock.Contains(date));

    /// <summary>The most recent Goal Race before <paramref name="race"/> by date; none if it is the first.</summary>
    public static GoalRace? PreviousRace(IEnumerable<GoalRace> races, GoalRace race) =>
        races.Where(r => r.Date < race.Date).MaxBy(r => r.Date);

    /// <summary>The Goal Race whose Training Block starts next after <paramref name="today"/>, if any.</summary>
    public static GoalRace? NextBlockRace(IEnumerable<GoalRace> races, DateOnly today) =>
        races.Where(r => r.TrainingBlock.Start > today).MinBy(r => r.TrainingBlock.Start);

    /// <summary>Days from <paramref name="today"/> to the Monday <paramref name="race"/>'s Training Block starts.</summary>
    public static int DaysUntilBlock(GoalRace race, DateOnly today) => race.TrainingBlock.Start.DayNumber - today.DayNumber;

    /// <summary>The first of the recent Training Weeks, which end with the one <paramref name="today"/> falls in.</summary>
    public static TrainingWeek FirstRecentWeek(DateOnly today) => TrainingWeek.Of(today).WeeksBefore(RecentWeekCount - 1);

    /// <summary>Distance (metres) of <paramref name="runs"/> in each Training Week from <paramref name="first"/> through <paramref name="last"/>.</summary>
    public static List<(TrainingWeek Week, double Distance)> DistanceByWeek(TrainingWeek first, TrainingWeek last, IEnumerable<RunSummary> runs)
    {
        var byWeek = runs.ToLookup(r => TrainingWeek.Of(r.StartLocal));
        return [.. TrainingWeek.Range(first, last).Select(w => (w, byWeek[w].Sum(r => r.Distance)))];
    }

    /// <summary>
    /// Every Training Week of <paramref name="block"/>, first to Race Week, with the actual values of
    /// <paramref name="runs"/> (by each Run's local start date) for the weeks that have begun by <paramref name="today"/>,
    /// and against <paramref name="plan"/> if the block has one.
    /// </summary>
    public static List<TrainingBlockWeek> WeeksOf(TrainingBlock block, IEnumerable<RunSummary> runs, DateOnly today, TrainingPlan? plan)
    {
        var byWeek = runs.ToLookup(r => TrainingWeek.Of(r.StartLocal));
        var currentWeek = TrainingWeek.Of(today);
        return [.. block.Weeks
            .Select(week =>
            {
                var weeksToRace = block.WeeksToRace(week);
                var status = week.Start > currentWeek.Start ? WeekStatus.Future
                    : week == currentWeek ? WeekStatus.Current
                    : WeekStatus.Past;
                var runsOfWeek = byWeek[week].ToList();
                var actual = status == WeekStatus.Future ? null : ActualOf(runsOfWeek);
                var weekPlan = plan is null ? null : PlanOf(plan, weeksToRace, week, status, runsOfWeek, actual, today);
                return new TrainingBlockWeek(weeksToRace, week, status, actual, weekPlan);
            })];
    }

    /// <summary>
    /// Two blocks' weeks side by side by Weeks to Race, one per Weeks to Race in either block, first to Race Week.
    /// A week only one block has is empty on the other side: a 16-week block next to a 14-week one has nothing to
    /// compare in weeks −15 and −14.
    /// </summary>
    public static List<LinedUpWeek> LineUp(IReadOnlyCollection<TrainingBlockWeek> viewed, IReadOnlyCollection<TrainingBlockWeek> compared)
    {
        var viewedByWeeksToRace = viewed.ToDictionary(w => w.WeeksToRace);
        var comparedByWeeksToRace = compared.ToDictionary(w => w.WeeksToRace);
        var first = viewedByWeeksToRace.Keys.Concat(comparedByWeeksToRace.Keys).Min();
        return [.. Enumerable.Range(first, 1 - first).Select(n => new LinedUpWeek(n, viewedByWeeksToRace.GetValueOrDefault(n), comparedByWeeksToRace.GetValueOrDefault(n)))];
    }

    /// <summary>The Long Run: the longest Run; of equally long ones, the one that took longer.</summary>
    public static RunSummary? LongRunOf(IEnumerable<RunSummary> runs) =>
        runs.OrderByDescending(r => r.Distance).ThenByDescending(r => r.MovingTime).ThenBy(r => r.Id).FirstOrDefault();

    /// <summary>
    /// Whole seconds per km by which <paramref name="pace"/> is slower (positive) or faster (negative) than
    /// <paramref name="targetPace"/>, both to the second as the Athlete reads them.
    /// </summary>
    public static int? PaceVsTarget(double? pace, int? targetPace) => pace is null ? null : (int)Math.Round(pace.Value) - targetPace;

    /// <summary>What <paramref name="plan"/> has for the week, against <paramref name="runs"/> in it.</summary>
    private static WeekPlan PlanOf(
        TrainingPlan plan,
        int weeksToRace,
        TrainingWeek week,
        WeekStatus status,
        IReadOnlyCollection<RunSummary> runs,
        WeekActual? actual,
        DateOnly today)
    {
        // A plan is as long as its Training Block, so it has every week of it.
        var planned = plan.Weeks.Single(w => w.WeeksToRace == weeksToRace);
        var runDays = runs.Select(r => DateOnly.FromDateTime(r.StartLocal)).ToHashSet();
        var days = Enumerable.Range(0, 7)
            .Select(i => week.Start.AddDays(i))
            .Select(date => new PlanDay(date, DayOf(date, planned.RunningDays.Any(d => d.Day == date.DayOfWeek), runDays.Contains(date), today)))
            .ToList();
        return new WeekPlan(
            plan.PhaseOf(weeksToRace),
            planned.RecoveryWeek,
            planned.PlannedDistance,
            planned.PlannedLongRun,
            Met(actual?.Distance, planned.PlannedDistance, status),
            // A week without Runs has a Long Run of nothing.
            Met(actual is null ? null : actual.LongRun?.Distance ?? 0, planned.PlannedLongRun, status),
            days);
    }

    private static DayState DayOf(DateOnly date, bool runningDay, bool ran, DateOnly today) =>
        (runningDay, ran) switch
        {
            (true, true) => DayState.Ran,
            (false, true) => DayState.Unplanned,
            (true, false) => date < today ? DayState.Missed : DayState.Planned,
            (false, false) => DayState.Rest,
        };

    /// <summary>
    /// Whether the <paramref name="actual"/> distance reached the <paramref name="planned"/> one, to the 10 m the
    /// Athlete reads: true once it has, false once the week is over without; none until then, or with nothing planned.
    /// </summary>
    private static bool? Met(double? actual, double? planned, WeekStatus status)
    {
        if (actual is null || planned is null)
            return null;
        if (TensOfMetres(actual.Value) >= TensOfMetres(planned.Value))
            return true;
        return status == WeekStatus.Past ? false : null;
    }

    /// <summary>To the 10 m of km with two decimals, rounding halves up as the frontend shows them.</summary>
    private static double TensOfMetres(double metres) => Math.Round(metres / 10, MidpointRounding.AwayFromZero);

    private static WeekActual ActualOf(List<RunSummary> runs)
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
