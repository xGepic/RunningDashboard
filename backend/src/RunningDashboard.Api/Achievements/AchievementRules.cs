using System.Globalization;
using RunningDashboard.Api.Records;
using RunningDashboard.Api.Training;

namespace RunningDashboard.Api.Achievements;

/// <summary>The Achievements there are and who earned them.</summary>
public static class AchievementRules
{
    /// <summary>Strava's names and distances (metres) of the Best Efforts that earn a Single Run Achievement.</summary>
    private static readonly (string Name, double Distance)[] SingleRun =
        [("10K", 10000), ("Half-Marathon", 21097), ("30K", 30000), ("Marathon", 42195), ("50K", 50000)];

    private static readonly int[] LifetimeKm = [100, 250, 500, 1000, 2000, 3000, 5000, 7500, 10000];
    private static readonly int[] YearKm = [500, 1000, 1500, 2000];
    private static readonly int[] MonthKm = [100, 150, 200, 250];
    private static readonly int[] WeekKm = [40, 60, 80, 100];

    /// <summary>The distances (metres) of the Best Efforts that earn a Single Run Achievement, to load only those.</summary>
    public static IReadOnlyCollection<double> SingleRunDistances { get; } = [.. SingleRun.Select(s => s.Distance)];

    /// <summary>
    /// Every Achievement, by group and threshold. A Run counts in the year, month and Training Week of its local start,
    /// but the Runs are added up in the order they were run (UTC), so a trip across time zones can't swap them.
    /// </summary>
    /// <param name="runs">Every Run.</param>
    /// <param name="efforts">The Best Efforts over <see cref="SingleRunDistances"/>.</param>
    internal static List<AchievementResponse> Of(IReadOnlyCollection<AchievementRun> runs, IReadOnlyCollection<RunBestEffort> efforts, DateOnly today)
    {
        var inOrder = runs.OrderBy(r => r.StartUtc).ThenBy(r => r.Id).ToList();
        double DistanceOf(Func<DateOnly, bool> day) => runs.Where(r => day(DateOnly.FromDateTime(r.StartLocal))).Sum(r => r.Distance);
        var thisWeek = TrainingWeek.Of(today);

        var singleRun = SingleRun.Select(s =>
        {
            var first = efforts.Where(e => e.Distance == s.Distance).OrderBy(e => e.StartUtc).ThenBy(e => e.RunId).FirstOrDefault();
            List<AchievementEarnedResponse> earned = first is null ? [] : [new(first.RunId, first.RunName, first.StartLocal)];
            return new AchievementResponse($"{AchievementGroup.SingleRun}-{s.Distance:0}", AchievementGroup.SingleRun, s.Name, s.Distance, false, earned, null);
        });

        var lifetime = LifetimeKm.Select(km =>
        {
            var crossing = Crossing(inOrder, km * 1000.0);
            List<AchievementEarnedResponse> earned = crossing is null ? [] : [Earned(crossing)];
            return new AchievementResponse($"{AchievementGroup.Lifetime}-{km * 1000}", AchievementGroup.Lifetime, Km(km), km * 1000.0, false, earned, null);
        });

        var year = Repeatable(AchievementGroup.Year, "year", YearKm, inOrder.GroupBy(r => r.StartLocal.Year));
        var month = Repeatable(AchievementGroup.Month, "month", MonthKm, inOrder.GroupBy(r => (r.StartLocal.Year, r.StartLocal.Month)));
        var week = Repeatable(AchievementGroup.Week, "week", WeekKm, inOrder.GroupBy(r => TrainingWeek.Of(r.StartLocal)));

        return
        [
            .. WithProgress(singleRun, runs.Count > 0 ? runs.Max(r => r.Distance) : 0),
            .. WithProgress(lifetime, runs.Sum(r => r.Distance)),
            .. WithProgress(year, DistanceOf(d => d.Year == today.Year)),
            .. WithProgress(month, DistanceOf(d => d.Year == today.Year && d.Month == today.Month)),
            .. WithProgress(week, DistanceOf(d => TrainingWeek.Of(d) == thisWeek)),
        ];
    }

    /// <summary>Gives the group's first Achievement never earned the <paramref name="progress"/> toward it.</summary>
    private static IEnumerable<AchievementResponse> WithProgress(IEnumerable<AchievementResponse> group, double progress)
    {
        var next = true;
        foreach (var achievement in group)
        {
            if (next && achievement.Earned.Count == 0)
            {
                next = false;
                yield return achievement with { Progress = progress };
            }
            else
            {
                yield return achievement;
            }
        }
    }

    /// <summary>Each threshold, earned by every year, month or Training Week (<paramref name="spans"/>) that reaches it.</summary>
    /// <param name="span">What the name calls the span, e.g. "1,000 km year".</param>
    /// <param name="spans">The Runs in the order they were run, grouped by span.</param>
    private static IEnumerable<AchievementResponse> Repeatable<TSpan>(
        string group, string span, int[] thresholdsKm, IEnumerable<IGrouping<TSpan, AchievementRun>> spans)
    {
        var inSpans = spans.ToList();
        return thresholdsKm.Select(km =>
        {
            var earned = inSpans.Select(span => Crossing(span, km * 1000.0)).OfType<AchievementRun>().Select(Earned).ToList();
            return new AchievementResponse($"{group}-{km * 1000}", group, $"{Km(km)} {span}", km * 1000.0, true, earned, null);
        });
    }

    /// <summary>The Run that takes the distance of <paramref name="runs"/>, in the order they were run, to <paramref name="threshold"/> or past it.</summary>
    private static AchievementRun? Crossing(IEnumerable<AchievementRun> runs, double threshold)
    {
        var total = 0.0;
        foreach (var run in runs)
        {
            total += run.Distance;
            if (total >= threshold)
                return run;
        }

        return null;
    }

    private static AchievementEarnedResponse Earned(AchievementRun run) => new(run.Id, run.Name, run.StartLocal);

    private static string Km(int km) => km.ToString("N0", CultureInfo.InvariantCulture) + " km";
}
