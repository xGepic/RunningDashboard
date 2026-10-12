using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Training;

namespace RunningDashboard.Api.Overview;

public static class OverviewEndpoints
{
    public static void MapOverviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/overview", async (AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var runs = await db.Runs
                .Select(r => new RunFigures(r.StartLocal, r.Distance, r.MovingTime, r.ElevationGain))
                .ToListAsync(cancellationToken);
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var goal = await db.GetYearlyGoalAsync(today.Year, cancellationToken);
            var recoveryWeeks = TrainingCalendar.RecoveryWeeks(await db.GoalRaces.ToListAsync(cancellationToken));
            return OverviewResponse.From(runs, goal is null ? null : YearlyGoalResponse.From(goal), recoveryWeeks, today);
        });
    }

    private sealed record RunFigures(DateTime StartLocal, double Distance, int MovingTime, double ElevationGain);

    /// <param name="Weeks">Every Training Week from the first Run's up to the current one, empty weeks as zero.</param>
    /// <param name="Months">Every month from the first Run's up to the current one.</param>
    /// <param name="YearlyGoal">The current year's; none without one.</param>
    /// <param name="Days">Every local date with Runs, oldest first.</param>
    /// <param name="Streak">The current and the longest Streak, over the whole history.</param>
    private sealed record OverviewResponse(
        TotalsResponse Totals,
        IReadOnlyList<YearResponse> Years,
        IReadOnlyList<WeekResponse> Weeks,
        IReadOnlyList<MonthResponse> Months,
        YearlyGoalResponse? YearlyGoal,
        IReadOnlyList<DayResponse> Days,
        StreakResponse Streak)
    {
        public static OverviewResponse From(
            IReadOnlyList<RunFigures> runs,
            YearlyGoalResponse? yearlyGoal,
            IReadOnlySet<TrainingWeek> recoveryWeeks,
            DateOnly today)
        {
            var totals = TotalsResponse.Of(runs);
            if (runs.Count == 0)
                return new OverviewResponse(totals, [], [], [], yearlyGoal, [], new StreakResponse(0, null));

            var years = runs
                .GroupBy(r => r.StartLocal.Year)
                .OrderBy(g => g.Key)
                .Select(g => YearResponse.Of(g.Key, TotalsResponse.Of([.. g])))
                .ToList();

            var distanceByWeek = runs
                .GroupBy(r => TrainingWeek.Of(r.StartLocal))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Distance));
            var firstWeek = distanceByWeek.Keys.Min(w => w.Start);
            var lastWeek = Max(distanceByWeek.Keys.Max(w => w.Start), TrainingWeek.Of(today).Start);
            var jumps = TrainingCalendar.RunWeekDistanceJumps(new TrainingWeek(firstWeek), new TrainingWeek(lastWeek), distanceByWeek, recoveryWeeks);
            var weeks = TrainingWeek.Range(new TrainingWeek(firstWeek), new TrainingWeek(lastWeek))
                .Select(w => new WeekResponse(w.Start, distanceByWeek.GetValueOrDefault(w), DistanceJumpResponse.From(jumps[w])))
                .ToList();

            var byMonth = runs.ToLookup(r => new DateOnly(r.StartLocal.Year, r.StartLocal.Month, 1));
            var firstMonth = byMonth.Min(g => g.Key);
            var lastMonth = Max(byMonth.Max(g => g.Key), new DateOnly(today.Year, today.Month, 1));
            var months = new List<MonthResponse>();
            for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
            {
                var inMonth = byMonth[month].ToList();
                var distance = inMonth.Sum(r => r.Distance);
                // Pace over the whole month: longer Runs weigh more than in an average of each Run's Pace.
                months.Add(new MonthResponse(month.Year, month.Month, distance, Run.PaceOf(inMonth.Sum(r => r.MovingTime), distance)));
            }

            // By the local start date, the same that decides a Run's Training Week.
            var days = runs
                .GroupBy(r => DateOnly.FromDateTime(r.StartLocal))
                .OrderBy(g => g.Key)
                .Select(g => new DayResponse(g.Key, g.Sum(r => r.Distance)))
                .ToList();

            // Up to the current week, so both Streaks look at the same weeks.
            var currentWeek = TrainingWeek.Of(today);
            var weeksWithRuns = distanceByWeek.Keys.Where(w => w.Start <= currentWeek.Start).ToHashSet();
            var streak = new StreakResponse(
                TrainingCalendar.CurrentStreak(weeksWithRuns, currentWeek),
                LongestStreakResponse.From(TrainingCalendar.LongestStreak(weeksWithRuns)));

            return new OverviewResponse(totals, years, weeks, months, yearlyGoal, days, streak);
        }

        private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    }

    /// <param name="Distance">Metres.</param>
    /// <param name="MovingTime">Seconds.</param>
    /// <param name="ElevationGain">Metres.</param>
    private sealed record TotalsResponse(int Runs, double Distance, int MovingTime, double ElevationGain)
    {
        public static TotalsResponse Of(IReadOnlyCollection<RunFigures> runs) =>
            new(runs.Count, runs.Sum(r => r.Distance), runs.Sum(r => r.MovingTime), runs.Sum(r => r.ElevationGain));
    }

    /// <summary>The same totals as <see cref="TotalsResponse"/>, for one calendar year (by local time).</summary>
    private sealed record YearResponse(int Year, int Runs, double Distance, int MovingTime, double ElevationGain)
    {
        public static YearResponse Of(int year, TotalsResponse totals) =>
            new(year, totals.Runs, totals.Distance, totals.MovingTime, totals.ElevationGain);
    }

    /// <param name="Start">The Monday of the Training Week.</param>
    /// <param name="DistanceJump">None unless the week is one.</param>
    private sealed record WeekResponse(DateOnly Start, double Distance, DistanceJumpResponse? DistanceJump);

    /// <param name="Pace">Seconds per kilometre over all of the month's Runs; none without Runs.</param>
    private sealed record MonthResponse(int Year, int Month, double Distance, double? Pace);

    /// <param name="Distance">Metres run that day, all its Runs together.</param>
    private sealed record DayResponse(DateOnly Date, double Distance);

    /// <param name="Current">Training Weeks; 0 without Runs.</param>
    /// <param name="Longest">None without Runs.</param>
    private sealed record StreakResponse(int Current, LongestStreakResponse? Longest);

    /// <param name="Weeks">Training Weeks.</param>
    /// <param name="First">The Monday of its first Training Week.</param>
    /// <param name="Last">The Monday of its last Training Week.</param>
    private sealed record LongestStreakResponse(int Weeks, DateOnly First, DateOnly Last)
    {
        public static LongestStreakResponse? From(Streak? streak) =>
            streak is null ? null : new(streak.Weeks, streak.First.Start, streak.Last.Start);
    }
}
