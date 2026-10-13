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
            return OverviewResponse.From(runs, goal is null ? null : YearlyGoalResponse.From(goal), today);
        });
    }

    private sealed record RunFigures(DateTime StartLocal, double Distance, int MovingTime, double ElevationGain);

    /// <param name="Weeks">Every Training Week from the first Run's up to the current one, empty weeks as zero.</param>
    /// <param name="Months">Every month from the first Run's up to the current one.</param>
    /// <param name="YearlyGoal">The current year's; none without one.</param>
    /// <param name="Streak">None without Runs.</param>
    private sealed record OverviewResponse(
        TotalsResponse Totals,
        IReadOnlyList<YearResponse> Years,
        IReadOnlyList<WeekResponse> Weeks,
        IReadOnlyList<MonthResponse> Months,
        YearlyGoalResponse? YearlyGoal,
        StreakResponse? Streak)
    {
        public static OverviewResponse From(IReadOnlyList<RunFigures> runs, YearlyGoalResponse? yearlyGoal, DateOnly today)
        {
            var totals = TotalsResponse.Of(runs);
            if (runs.Count == 0)
                return new OverviewResponse(totals, [], [], [], yearlyGoal, null);

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

            return new OverviewResponse(totals, years, weeks, months, yearlyGoal, streak);
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
    private sealed record WeekResponse(DateOnly Start, double Distance);

    /// <summary>
    /// The Streak: consecutive Training Weeks with at least one Run. The current week adds to it once it has a Run and
    /// only breaks it once it ends without one.
    /// </summary>
    /// <param name="Current">Weeks in the Streak up to now.</param>
    /// <param name="Longest">The Longest Streak; the more recent of two equally long ones, so the ongoing Streak once it ties.</param>
    /// <param name="RecentWeeks">The last <see cref="RecentWeekCount"/> Training Weeks up to the current one, oldest first, but none before the first Run's.</param>
    private sealed record StreakResponse(int Current, bool ThisWeekHasRun, LongestStreakResponse Longest, IReadOnlyList<StreakWeekResponse> RecentWeeks)
    {
        private const int RecentWeekCount = 12;

        /// <param name="weeksWithRuns">At least one.</param>
        public static StreakResponse Of(HashSet<TrainingWeek> weeksWithRuns, TrainingWeek thisWeek)
        {
            var thisWeekHasRun = weeksWithRuns.Contains(thisWeek);
            var current = 0;
            for (var week = thisWeekHasRun ? thisWeek : thisWeek.WeeksBefore(1); weeksWithRuns.Contains(week); week = week.WeeksBefore(1))
                current++;

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

            return new StreakResponse(current, thisWeekHasRun, longest, recentWeeks);
        }
    }

    /// <param name="Start">The Monday of its first Training Week.</param>
    /// <param name="End">The Sunday of its last Training Week.</param>
    private sealed record LongestStreakResponse(int Weeks, DateOnly Start, DateOnly End);

    /// <param name="Start">The Monday of the Training Week.</param>
    private sealed record StreakWeekResponse(DateOnly Start, bool HasRun);

    /// <param name="Pace">Seconds per kilometre over all of the month's Runs; none without Runs.</param>
    private sealed record MonthResponse(int Year, int Month, double Distance, double? Pace);
}
