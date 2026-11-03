using RunningDashboard.Api.Journey;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Training;

namespace RunningDashboard.Api.Overview;

internal sealed record RunFigures(DateTime StartLocal, double Distance, int MovingTime, double ElevationGain);

/// <summary>A Ride or a Swim, for Training Time only.</summary>
/// <param name="MovingTime">Seconds.</param>
internal sealed record ListedFigures(DateTime StartLocal, int MovingTime, string SportType);

/// <param name="Weeks">Every Training Week from the first Run's up to the current one, empty weeks as zero.</param>
/// <param name="YearlyGoal">The current year's; none without one.</param>
/// <param name="WeeklyTimeGoal">None without one.</param>
/// <param name="Streak">None without Runs.</param>
/// <param name="TrainingTime">
/// Every Training Week from the first Run's or Listed Activity's up to the current one, empty weeks as zero; empty
/// without Runs or without Rides and Swims, as there is nothing to add to the running figures then.
/// </param>
/// <param name="ThisWeek">The current Training Week's Training Time, for the Weekly Time Goal; none without Runs.</param>
/// <param name="OnThisDay">Today's date a year ago; none without Runs on it.</param>
/// <param name="Journey">The current year on the Virtual Journey.</param>
internal sealed record OverviewResponse(
    TotalsResponse Totals,
    IReadOnlyList<YearResponse> Years,
    IReadOnlyList<WeekResponse> Weeks,
    YearlyGoalResponse? YearlyGoal,
    WeeklyTimeGoalResponse? WeeklyTimeGoal,
    StreakResponse? Streak,
    IReadOnlyList<TrainingTimeWeekResponse> TrainingTime,
    TrainingTimeWeekResponse? ThisWeek,
    OnThisDayResponse? OnThisDay,
    JourneyResponse Journey)
{
    public static OverviewResponse From(
        IReadOnlyList<RunFigures> runs,
        IReadOnlyList<ListedFigures> listed,
        YearlyGoalResponse? yearlyGoal,
        WeeklyTimeGoalResponse? weeklyTimeGoal,
        OnThisDayResponse? onThisDay,
        JourneyResponse journey,
        DateOnly today)
    {
        var totals = TotalsResponse.Of(runs);
        if (runs.Count == 0)
            return new OverviewResponse(totals, [], [], yearlyGoal, weeklyTimeGoal, null, [], null, onThisDay, journey);

        var years = runs
            .GroupBy(r => r.StartLocal.Year)
            .OrderBy(g => g.Key)
            .Select(g => YearResponse.Of(g.Key, TotalsResponse.Of([.. g])))
            .ToList();

        var weeksWithRuns = runs.Select(r => TrainingWeek.Of(r.StartLocal)).ToHashSet();
        var streak = StreakResponse.Of(weeksWithRuns, TrainingWeek.Of(today));

        var distanceByWeek = runs
            .GroupBy(r => TrainingWeek.Of(r.StartLocal))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Distance));
        var firstWeek = distanceByWeek.Keys.Min(w => w.Start);
        var lastWeek = Max(distanceByWeek.Keys.Max(w => w.Start), TrainingWeek.Of(today).Start);
        var weeks = TrainingWeek.Range(new TrainingWeek(firstWeek), new TrainingWeek(lastWeek))
            .Select(w => new WeekResponse(w.Start, distanceByWeek.GetValueOrDefault(w)))
            .ToList();

        var thisWeek = TrainingWeek.Of(today);
        return new OverviewResponse(
            totals,
            years,
            weeks,
            yearlyGoal,
            weeklyTimeGoal,
            streak,
            TrainingTimeWeekResponse.Of(runs, listed, thisWeek),
            TrainingTimeWeekResponse.For(thisWeek, runs, listed),
            onThisDay,
            journey);
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}

/// <summary>Training Time: the Moving Time of one Training Week's Runs, Rides and Swims, each on its own.</summary>
/// <param name="Start">The Monday of the Training Week.</param>
/// <param name="Runs">Seconds.</param>
/// <param name="Rides">Seconds.</param>
/// <param name="Swims">Seconds.</param>
internal sealed record TrainingTimeWeekResponse(DateOnly Start, int Runs, int Rides, int Swims)
{
    /// <param name="runs">At least one.</param>
    public static List<TrainingTimeWeekResponse> Of(IReadOnlyList<RunFigures> runs, IReadOnlyList<ListedFigures> listed, TrainingWeek thisWeek)
    {
        if (listed.Count == 0)
            return [];

        var runTime = runs.GroupBy(r => TrainingWeek.Of(r.StartLocal)).ToDictionary(g => g.Key, g => g.Sum(r => r.MovingTime));
        var rideTime = TimeByWeek(listed.Where(a => Sports.Rides.Contains(a.SportType)));
        var swimTime = TimeByWeek(listed.Where(a => Sports.Swims.Contains(a.SportType)));
        var weeksWithActivities = runTime.Keys.Concat(rideTime.Keys).Concat(swimTime.Keys).ToList();
        var firstWeek = weeksWithActivities.MinBy(w => w.Start);
        // A Run or Ride abroad can already be in the next week by its local time.
        var lastWeek = weeksWithActivities.Append(thisWeek).MaxBy(w => w.Start);
        return [.. TrainingWeek.Range(firstWeek, lastWeek).Select(w => new TrainingTimeWeekResponse(w.Start, runTime.GetValueOrDefault(w), rideTime.GetValueOrDefault(w), swimTime.GetValueOrDefault(w)))];
    }

    /// <summary>The Training Time of <paramref name="week"/> alone, Rides and Swims or not.</summary>
    public static TrainingTimeWeekResponse For(TrainingWeek week, IReadOnlyList<RunFigures> runs, IReadOnlyList<ListedFigures> listed)
    {
        var inWeek = listed.Where(a => TrainingWeek.Of(a.StartLocal) == week).ToList();
        return new TrainingTimeWeekResponse(
            week.Start,
            runs.Where(r => TrainingWeek.Of(r.StartLocal) == week).Sum(r => r.MovingTime),
            inWeek.Where(a => Sports.Rides.Contains(a.SportType)).Sum(a => a.MovingTime),
            inWeek.Where(a => Sports.Swims.Contains(a.SportType)).Sum(a => a.MovingTime));
    }

    private static Dictionary<TrainingWeek, int> TimeByWeek(IEnumerable<ListedFigures> activities) =>
        activities.GroupBy(a => TrainingWeek.Of(a.StartLocal)).ToDictionary(g => g.Key, g => g.Sum(a => a.MovingTime));
}

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="ElevationGain">Metres.</param>
internal sealed record TotalsResponse(int Runs, double Distance, int MovingTime, double ElevationGain)
{
    public static TotalsResponse Of(IReadOnlyCollection<RunFigures> runs) =>
        new(runs.Count, runs.Sum(r => r.Distance), runs.Sum(r => r.MovingTime), runs.Sum(r => r.ElevationGain));
}

/// <summary>The same totals as <see cref="TotalsResponse"/>, for one calendar year (by local time).</summary>
internal sealed record YearResponse(int Year, int Runs, double Distance, int MovingTime, double ElevationGain)
{
    public static YearResponse Of(int year, TotalsResponse totals) =>
        new(year, totals.Runs, totals.Distance, totals.MovingTime, totals.ElevationGain);
}

/// <param name="Start">The Monday of the Training Week.</param>
internal sealed record WeekResponse(DateOnly Start, double Distance);

/// <summary>Today's date a year ago, by local time; on 29 February, 28 February a year ago.</summary>
/// <param name="Runs">At least one, newest first.</param>
internal sealed record OnThisDayResponse(DateOnly Day, IReadOnlyList<OnThisDayRunResponse> Runs);

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
internal sealed record OnThisDayRunResponse(long Id, string Name, DateTime StartLocal, double Distance, int MovingTime);

/// <summary>
/// The Streak: consecutive Training Weeks with at least one Run. The current week adds to it once it has a Run and
/// only breaks it once it ends without one.
/// </summary>
/// <param name="Current">Weeks in the Streak up to now.</param>
/// <param name="Start">The Monday of the current Streak's first Training Week; none while it is 0.</param>
/// <param name="Longest">The Longest Streak; the more recent of two equally long ones, so the ongoing Streak once it ties.</param>
/// <param name="RecentWeeks">The last <see cref="RecentWeekCount"/> Training Weeks up to the current one, oldest first, but none before the first Run's.</param>
internal sealed record StreakResponse(int Current, DateOnly? Start, bool ThisWeekHasRun, LongestStreakResponse Longest, IReadOnlyList<StreakWeekResponse> RecentWeeks)
{
    private const int RecentWeekCount = 12;

    /// <param name="weeksWithRuns">At least one.</param>
    public static StreakResponse Of(HashSet<TrainingWeek> weeksWithRuns, TrainingWeek thisWeek)
    {
        var thisWeekHasRun = weeksWithRuns.Contains(thisWeek);
        var current = 0;
        DateOnly? start = null;
        for (var week = thisWeekHasRun ? thisWeek : thisWeek.WeeksBefore(1); weeksWithRuns.Contains(week); week = week.WeeksBefore(1))
        {
            current++;
            start = week.Start;
        }

        var firstWeek = weeksWithRuns.MinBy(w => w.Start);
        var longest = new LongestStreakResponse(0, default, default);
        var length = 0;
        // Up to this week only: a Run abroad can already be in the next one by its local time.
        foreach (var week in TrainingWeek.Range(firstWeek, thisWeek))
        {
            length = weeksWithRuns.Contains(week) ? length + 1 : 0;
            if (length > 0 && length >= longest.Weeks)
                longest = new LongestStreakResponse(length, week.WeeksBefore(length - 1).Start, week.End);
        }

        var firstRecent = thisWeek.WeeksBefore(RecentWeekCount - 1);
        var recentWeeks = TrainingWeek.Range(firstRecent.Start > firstWeek.Start ? firstRecent : firstWeek, thisWeek)
            .Select(w => new StreakWeekResponse(w.Start, weeksWithRuns.Contains(w)))
            .ToList();

        return new StreakResponse(current, start, thisWeekHasRun, longest, recentWeeks);
    }
}

/// <param name="Start">The Monday of its first Training Week.</param>
/// <param name="End">The Sunday of its last Training Week.</param>
internal sealed record LongestStreakResponse(int Weeks, DateOnly Start, DateOnly End);

/// <param name="Start">The Monday of the Training Week.</param>
internal sealed record StreakWeekResponse(DateOnly Start, bool HasRun);
