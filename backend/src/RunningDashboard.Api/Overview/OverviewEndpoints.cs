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
    private sealed record OverviewResponse(
        TotalsResponse Totals,
        IReadOnlyList<YearResponse> Years,
        IReadOnlyList<WeekResponse> Weeks,
        IReadOnlyList<MonthResponse> Months,
        YearlyGoalResponse? YearlyGoal)
    {
        public static OverviewResponse From(
            IReadOnlyList<RunFigures> runs,
            YearlyGoalResponse? yearlyGoal,
            IReadOnlySet<TrainingWeek> recoveryWeeks,
            DateOnly today)
        {
            var totals = TotalsResponse.Of(runs);
            if (runs.Count == 0)
                return new OverviewResponse(totals, [], [], [], yearlyGoal);

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
            var distanceWeeks = TrainingWeek.Range(new TrainingWeek(firstWeek), new TrainingWeek(lastWeek))
                .Select(w => new DistanceWeek(w, distanceByWeek.TryGetValue(w, out var distance) ? distance : null, recoveryWeeks.Contains(w)))
                .ToList();
            var weeks = distanceWeeks
                .Zip(TrainingCalendar.DistanceJumps(distanceWeeks), (w, jump) => new WeekResponse(w.Week.Start, w.Distance ?? 0, DistanceJumpResponse.From(jump)))
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

            return new OverviewResponse(totals, years, weeks, months, yearlyGoal);
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
}
