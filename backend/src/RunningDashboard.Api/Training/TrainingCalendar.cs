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
/// <param name="RunType">The Running Day's; none for a rest day or a Run on an unplanned day.</param>
public sealed record PlanDay(DateOnly Date, DayState State, RunType? RunType);

/// <summary>A planned Training Week against what was run in it.</summary>
/// <param name="PlannedDistance">Metres; none if not planned.</param>
/// <param name="PlannedLongRun">Metres; none if not planned.</param>
/// <param name="DistanceMet">
/// True once the week's distance reached the planned one, in km to two decimals as the Athlete reads them; false
/// once a week is over without; none while it still runs short, for a future week, or without a planned distance.
/// </param>
/// <param name="LongRunMet">Likewise for the Long Run.</param>
/// <param name="Days">Monday to Sunday.</param>
/// <param name="DistanceJump">None unless the planned distance makes the week one, against the planned weeks before it.</param>
public sealed record WeekPlan(
    PhaseType Phase,
    bool RecoveryWeek,
    double? PlannedDistance,
    double? PlannedLongRun,
    bool? DistanceMet,
    bool? LongRunMet,
    IReadOnlyList<PlanDay> Days,
    DistanceJump? DistanceJump);

/// <summary>One Weeks to Race across two Training Blocks; either block may not have that week.</summary>
public sealed record LinedUpWeek(int WeeksToRace, TrainingBlockWeek? Viewed, TrainingBlockWeek? Compared);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="RelativeEffort">Sum over the week's Runs that have one; none if no Run has.</param>
/// <param name="LongRun">The longest Run of the week; none in a week without Runs.</param>
/// <param name="DistanceJump">None unless the week's distance so far makes it one.</param>
public sealed record WeekActual(int Runs, double Distance, int MovingTime, double? RelativeEffort, RunSummary? LongRun, DistanceJump? DistanceJump);

/// <summary>One of consecutive Training Weeks, as the Distance Jump rule sees it.</summary>
/// <param name="Distance">Metres run, or planned; none for a week without Runs, or without a planned distance.</param>
public sealed record DistanceWeek(TrainingWeek Week, double? Distance, bool RecoveryWeek);

/// <summary>A Training Week more than 10% above the week it is compared with.</summary>
/// <param name="ComparedWith">The week before it, or, past Recovery Weeks, the last one before them.</param>
/// <param name="Increase">The ratio of the distances minus one: 0.2 for 20% up.</param>
public sealed record DistanceJump(TrainingWeek ComparedWith, double Increase);

/// <summary>Consecutive Training Weeks with at least one Run each, from <paramref name="First"/> to <paramref name="Last"/>.</summary>
public sealed record Streak(TrainingWeek First, TrainingWeek Last)
{
    /// <summary>How many Training Weeks it lasted.</summary>
    public int Weeks => (Last.Start.DayNumber - First.Start.DayNumber) / 7 + 1;
}

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

    /// <summary>
    /// Distance (metres) of <paramref name="runs"/> in each Training Week from <paramref name="first"/> through
    /// <paramref name="last"/>, with its Distance Jump. The runs reach back to <see cref="LookBackFrom"/>
    /// <paramref name="first"/>, so that the first week is judged too.
    /// </summary>
    public static List<(TrainingWeek Week, double Distance, DistanceJump? DistanceJump)> DistanceByWeek(
        TrainingWeek first,
        TrainingWeek last,
        IEnumerable<RunSummary> runs,
        IReadOnlySet<TrainingWeek> recoveryWeeks)
    {
        var distances = DistanceOfWeeks(runs);
        var jumps = RunWeekDistanceJumps(first, last, distances, recoveryWeeks);
        return [.. TrainingWeek.Range(first, last).Select(w => (w, distances.GetValueOrDefault(w), jumps[w]))];
    }

    /// <summary>
    /// Every Training Week of <paramref name="block"/>, first to Race Week, with the actual values of
    /// <paramref name="runs"/> (by each Run's local start date) for the weeks that have begun by <paramref name="today"/>,
    /// and against <paramref name="plan"/> if the block has one. The runs reach back to <see cref="LookBackFrom"/> the
    /// block's first week, so that its Distance Jump is judged too.
    /// </summary>
    public static List<TrainingBlockWeek> WeeksOf(
        TrainingBlock block,
        IReadOnlyCollection<RunSummary> runs,
        DateOnly today,
        TrainingPlan? plan,
        IReadOnlySet<TrainingWeek> recoveryWeeks)
    {
        var byWeek = runs.ToLookup(r => TrainingWeek.Of(r.StartLocal));
        var jumps = RunWeekDistanceJumps(block.FirstWeek, block.RaceWeek, DistanceOfWeeks(runs), recoveryWeeks);
        Dictionary<int, DistanceJump?> plannedJumps = plan is null ? [] : PlannedDistanceJumps(plan, block);
        var currentWeek = TrainingWeek.Of(today);
        return [.. block.Weeks
            .Select(week =>
            {
                var weeksToRace = block.WeeksToRace(week);
                var status = week.Start > currentWeek.Start ? WeekStatus.Future
                    : week == currentWeek ? WeekStatus.Current
                    : WeekStatus.Past;
                var runsOfWeek = byWeek[week].ToList();
                var actual = status == WeekStatus.Future ? null : ActualOf(runsOfWeek, jumps[week]);
                var weekPlan = plan is null ? null : PlanOf(plan, weeksToRace, week, status, runsOfWeek, actual, plannedJumps[weeksToRace], today);
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

    /// <summary>
    /// The Training Weeks that are Recovery Weeks: those in a Goal Race's Training Block whose Training Plan marks
    /// that Weeks to Race as one. Weeks outside a block, or in one without a plan, never are.
    /// </summary>
    public static HashSet<TrainingWeek> RecoveryWeeks(IEnumerable<GoalRace> races) =>
        [.. races
            .Where(r => r.TrainingPlan is not null)
            .SelectMany(r => r.TrainingPlan!.Weeks.Where(w => w.RecoveryWeek).Select(w => r.TrainingBlock.WeekAt(w.WeeksToRace)))];

    /// <summary>
    /// The earliest Training Week that <paramref name="first"/>'s Distance Jump may be compared with: the week before
    /// it, or, past Recovery Weeks, the last one before them.
    /// </summary>
    public static TrainingWeek LookBackFrom(TrainingWeek first, IReadOnlySet<TrainingWeek> recoveryWeeks)
    {
        var week = first.WeeksBefore(1);
        while (recoveryWeeks.Contains(week))
            week = week.WeeksBefore(1);
        return week;
    }

    /// <summary>
    /// The Distance Jump of each Training Week from <paramref name="first"/> through <paramref name="last"/> in what
    /// was run. <paramref name="distanceByWeek"/> has the metres of the weeks with Runs, back to
    /// <see cref="LookBackFrom"/> <paramref name="first"/>; a week it leaves out had none.
    /// </summary>
    public static Dictionary<TrainingWeek, DistanceJump?> RunWeekDistanceJumps(
        TrainingWeek first,
        TrainingWeek last,
        IReadOnlyDictionary<TrainingWeek, double> distanceByWeek,
        IReadOnlySet<TrainingWeek> recoveryWeeks)
    {
        var weeks = TrainingWeek.Range(LookBackFrom(first, recoveryWeeks), last)
            .Select(w => new DistanceWeek(w, distanceByWeek.TryGetValue(w, out var distance) ? distance : null, recoveryWeeks.Contains(w)))
            .ToList();
        return weeks
            .Zip(DistanceJumps(weeks), (week, jump) => (week.Week, Jump: jump))
            .Where(w => w.Week.Start >= first.Start)
            .ToDictionary(w => w.Week, w => w.Jump);
    }

    /// <summary>
    /// The planned Distance Jump of each week of <paramref name="plan"/>, by Weeks to Race: its planned distance
    /// against the planned weeks before it, never against what was run or weeks before the plan.
    /// </summary>
    public static Dictionary<int, DistanceJump?> PlannedDistanceJumps(TrainingPlan plan, TrainingBlock block)
    {
        var weeks = plan.Weeks.OrderBy(w => w.WeeksToRace).ToList();
        var jumps = DistanceJumps([.. weeks.Select(w => new DistanceWeek(block.WeekAt(w.WeeksToRace), w.PlannedDistance, w.RecoveryWeek))]);
        return weeks.Zip(jumps).ToDictionary(w => w.First.WeeksToRace, w => w.Second);
    }

    /// <summary>
    /// The Distance Jump of each of <paramref name="weeks"/>, which follow each other; none for a week that is not
    /// one. A week is one if its distance is more than 1.10 times that of the week it is compared with: the week
    /// before, skipping back past Recovery Weeks. There is none without such a week, if that week has no distance, or
    /// if the week itself has none. Weeks are only compared with weeks in the list, so to judge the first week shown,
    /// start the list earlier.
    /// </summary>
    public static List<DistanceJump?> DistanceJumps(IReadOnlyList<DistanceWeek> weeks)
    {
        var jumps = new List<DistanceJump?>(weeks.Count);
        for (var i = 0; i < weeks.Count; i++)
        {
            var compared = i - 1;
            while (compared >= 0 && weeks[compared].RecoveryWeek)
                compared--;
            var before = compared >= 0 ? weeks[compared].Distance : null;
            // Multiplied out, so that exactly 10% up is not a jump: 1.10 has no exact binary fraction.
            jumps.Add(before > 0 && weeks[i].Distance is { } distance && distance * 10 > before * 11
                ? new DistanceJump(weeks[compared].Week, distance / before.Value - 1)
                : null);
        }

        return jumps;
    }

    /// <summary>
    /// The current Streak in Training Weeks, counted back from <paramref name="currentWeek"/>. The current week adds to
    /// it once it has a Run, and only breaks it once its Sunday has passed without one; any earlier week without a Run
    /// ends it.
    /// </summary>
    public static int CurrentStreak(IReadOnlySet<TrainingWeek> weeksWithRuns, TrainingWeek currentWeek)
    {
        var week = weeksWithRuns.Contains(currentWeek) ? currentWeek : currentWeek.WeeksBefore(1);
        var weeks = 0;
        while (weeksWithRuns.Contains(week))
        {
            weeks++;
            week = week.WeeksBefore(1);
        }

        return weeks;
    }

    /// <summary>The longest Streak in <paramref name="weeksWithRuns"/>; of equally long ones, the most recent; none without Runs.</summary>
    public static Streak? LongestStreak(IEnumerable<TrainingWeek> weeksWithRuns)
    {
        Streak? longest = null;
        Streak? streak = null;
        foreach (var week in weeksWithRuns.OrderBy(w => w.Start))
        {
            streak = streak is not null && streak.Last.Next == week ? streak with { Last = week } : new Streak(week, week);
            // Later Streaks come last, so a tie goes to the most recent.
            if (longest is null || streak.Weeks >= longest.Weeks)
                longest = streak;
        }

        return longest;
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
        DistanceJump? distanceJump,
        DateOnly today)
    {
        // A plan is as long as its Training Block, so it has every week of it.
        var planned = plan.Weeks.Single(w => w.WeeksToRace == weeksToRace);
        var runDays = runs.Select(r => DateOnly.FromDateTime(r.StartLocal)).ToHashSet();
        var days = Enumerable.Range(0, 7)
            .Select(i => week.Start.AddDays(i))
            .Select(date => (Date: date, planned.RunningDays.SingleOrDefault(d => d.Day == date.DayOfWeek)?.RunType))
            .Select(day => new PlanDay(day.Date, DayOf(day.Date, day.RunType is not null, runDays.Contains(day.Date), today), day.RunType))
            .ToList();
        return new WeekPlan(
            plan.PhaseOf(weeksToRace),
            planned.RecoveryWeek,
            planned.PlannedDistance,
            planned.PlannedLongRun,
            Met(actual?.Distance, planned.PlannedDistance, status),
            // A week without Runs has a Long Run of nothing.
            Met(actual is null ? null : actual.LongRun?.Distance ?? 0, planned.PlannedLongRun, status),
            days,
            distanceJump);
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

    /// <summary>Metres run in each Training Week that has Runs.</summary>
    private static Dictionary<TrainingWeek, double> DistanceOfWeeks(IEnumerable<RunSummary> runs) =>
        runs.GroupBy(r => TrainingWeek.Of(r.StartLocal)).ToDictionary(g => g.Key, g => g.Sum(r => r.Distance));

    private static WeekActual ActualOf(List<RunSummary> runs, DistanceJump? distanceJump)
    {
        var efforts = runs.Where(r => r.RelativeEffort is not null).Select(r => r.RelativeEffort!.Value).ToList();
        return new WeekActual(
            runs.Count,
            runs.Sum(r => r.Distance),
            runs.Sum(r => r.MovingTime),
            efforts.Count > 0 ? efforts.Sum() : null,
            LongRunOf(runs),
            distanceJump);
    }
}
