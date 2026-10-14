using RunningDashboard.Api.GoalRaces;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Records;
using RunningDashboard.Api.Training;

namespace RunningDashboard.Api.Review;

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="ElevationGain">Metres.</param>
internal sealed record ReviewRun(long Id, string Name, DateTime StartLocal, double Distance, int MovingTime, double ElevationGain, string? ShoeId);

/// <summary>
/// A calendar year's highlights. Training Weeks across New Year count only with their days inside the year, so no Run
/// counts in two years and the year's first and last weeks can be shorter than seven days.
/// </summary>
/// <param name="From">1 Jan, or the first Run's day when the history starts within the year.</param>
/// <param name="Until">31 Dec, or today for the current year.</param>
/// <param name="PreviousYear">
/// The year before, up to the same day as <paramref name="Until"/>, so the current year so far is compared with the
/// same time last year; none without Runs then.
/// </param>
/// <param name="PersonalBests">For each distance, the year's fastest Best Effort, if it was a Personal Best when run.</param>
/// <param name="LongestStreak">The longest Streak within the year; none without Runs.</param>
/// <param name="MostUsedShoe">The Shoe with the most distance in the year's Runs; none without a Shoe.</param>
/// <param name="Rides">Listed, not counted toward anything.</param>
/// <param name="Swims">Listed, not counted toward anything.</param>
internal sealed record YearReviewResponse(
    int Year,
    DateOnly From,
    DateOnly Until,
    ReviewTotalsResponse Totals,
    ReviewTotalsResponse? PreviousYear,
    ReviewRunResponse? LongestRun,
    ReviewWeekResponse? BiggestWeek,
    ReviewMonthResponse? BiggestMonth,
    IReadOnlyList<ReviewPersonalBestResponse> PersonalBests,
    ReviewStreakResponse? LongestStreak,
    IReadOnlyList<ReviewGoalRaceResponse> GoalRaces,
    ReviewShoeResponse? MostUsedShoe,
    int Rides,
    int Swims)
{
    /// <param name="runs">The year's Runs and the year before's.</param>
    /// <param name="efforts">Every Best Effort up to the year's end.</param>
    public static YearReviewResponse Of(
        int year,
        DateOnly today,
        DateTime? firstRun,
        IReadOnlyList<ReviewRun> runs,
        IReadOnlyList<RunBestEffort> efforts,
        IReadOnlyList<ReviewGoalRaceResponse> goalRaces,
        IReadOnlyDictionary<string, string> shoeNames,
        int rides,
        int swims)
    {
        var firstDay = new DateOnly(year, 1, 1);
        var lastDay = new DateOnly(year, 12, 31);
        var from = firstRun is { } first && DateOnly.FromDateTime(first) > firstDay && first.Year == year ? DateOnly.FromDateTime(first) : firstDay;
        var until = today.Year == year ? today : lastDay;

        var inYear = runs.Where(r => r.StartLocal.Year == year).ToList();
        var sameTimeLastYear = until.AddYears(-1);
        var lastYear = runs.Where(r => r.StartLocal.Year == year - 1 && DateOnly.FromDateTime(r.StartLocal) <= sameTimeLastYear).ToList();

        return new YearReviewResponse(
            year,
            from,
            until,
            ReviewTotalsResponse.Of(inYear),
            lastYear.Count > 0 ? ReviewTotalsResponse.Of(lastYear) : null,
            inYear.MaxBy(r => r.Distance) is { } longest ? ReviewRunResponse.From(longest) : null,
            ReviewWeekResponse.Biggest(inYear, firstDay, lastDay),
            ReviewMonthResponse.Biggest(inYear),
            ReviewPersonalBestResponse.SetIn(year, efforts),
            ReviewStreakResponse.Longest(inYear, firstDay, lastDay, today),
            goalRaces,
            ReviewShoeResponse.MostUsed(inYear, shoeNames),
            rides,
            swims);
    }
}

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="ElevationGain">Metres.</param>
internal sealed record ReviewTotalsResponse(int Runs, double Distance, int MovingTime, double ElevationGain)
{
    public static ReviewTotalsResponse Of(IReadOnlyCollection<ReviewRun> runs) =>
        new(runs.Count, runs.Sum(r => r.Distance), runs.Sum(r => r.MovingTime), runs.Sum(r => r.ElevationGain));
}

/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
internal sealed record ReviewRunResponse(long Id, string Name, DateTime StartLocal, double Distance, int MovingTime)
{
    public static ReviewRunResponse From(ReviewRun run) => new(run.Id, run.Name, run.StartLocal, run.Distance, run.MovingTime);
}

/// <summary>The Training Week with the most distance, its days cut to the year's.</summary>
/// <param name="Start">The Monday, or 1 Jan for a week that began the year before.</param>
/// <param name="End">The Sunday, or 31 Dec for a week that ends the year after.</param>
/// <param name="Distance">Metres.</param>
internal sealed record ReviewWeekResponse(DateOnly Start, DateOnly End, int Runs, double Distance)
{
    public static ReviewWeekResponse? Biggest(IReadOnlyCollection<ReviewRun> runs, DateOnly firstDay, DateOnly lastDay) =>
        runs
            .GroupBy(r => TrainingWeek.Of(r.StartLocal))
            .Select(g => new ReviewWeekResponse(Max(g.Key.Start, firstDay), Min(g.Key.End, lastDay), g.Count(), g.Sum(r => r.Distance)))
            .OrderByDescending(w => w.Distance)
            .ThenBy(w => w.Start)
            .FirstOrDefault();

    internal static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    internal static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}

/// <param name="Month">1 for January.</param>
/// <param name="Distance">Metres.</param>
internal sealed record ReviewMonthResponse(int Month, int Runs, double Distance)
{
    public static ReviewMonthResponse? Biggest(IReadOnlyCollection<ReviewRun> runs) =>
        runs
            .GroupBy(r => r.StartLocal.Month)
            .Select(g => new ReviewMonthResponse(g.Key, g.Count(), g.Sum(r => r.Distance)))
            .OrderByDescending(m => m.Distance)
            .ThenBy(m => m.Month)
            .FirstOrDefault();
}

/// <param name="Name">Strava's name for the distance, e.g. "5K".</param>
/// <param name="Distance">Metres.</param>
/// <param name="ElapsedTime">Seconds.</param>
/// <param name="PreviousElapsedTime">The Personal Best the year started with; none if the year set the first.</param>
internal sealed record ReviewPersonalBestResponse(
    string Name, double Distance, int ElapsedTime, int? PreviousElapsedTime, long RunId, string RunName, DateTime StartLocal)
{
    /// <summary>
    /// The last step of each distance's Personal Best progression within the year: the year's fastest Best Effort,
    /// since each step is faster than every earlier one.
    /// </summary>
    public static List<ReviewPersonalBestResponse> SetIn(int year, IReadOnlyCollection<RunBestEffort> efforts) =>
        [.. efforts
            .GroupBy(e => e.Distance)
            .OrderBy(g => g.Key)
            .Select(g => PersonalBests.Progression(g))
            .Where(steps => steps.Any(s => s.StartLocal.Year == year))
            .Select(steps =>
            {
                var best = steps.Last(s => s.StartLocal.Year == year);
                var before = steps.LastOrDefault(s => s.StartLocal.Year < year);
                return new ReviewPersonalBestResponse(
                    best.Name, best.Distance, best.ElapsedTime, before?.ElapsedTime, best.RunId, best.RunName, best.StartLocal);
            })];
}

/// <summary>The longest Streak within the year, its first and last weeks cut to the year's days.</summary>
/// <param name="Start">The Monday of its first Training Week, or 1 Jan.</param>
/// <param name="End">The Sunday of its last Training Week, or 31 Dec.</param>
internal sealed record ReviewStreakResponse(int Weeks, DateOnly Start, DateOnly End)
{
    /// <summary>Of two equally long ones, the more recent, as with the Longest Streak.</summary>
    public static ReviewStreakResponse? Longest(IReadOnlyCollection<ReviewRun> runs, DateOnly firstDay, DateOnly lastDay, DateOnly today)
    {
        if (runs.Count == 0)
            return null;

        var weeksWithRuns = runs.Select(r => TrainingWeek.Of(r.StartLocal)).ToHashSet();
        // Up to this week at most: a Run abroad can already be in the next one by its local time.
        var lastWeek = TrainingWeek.Of(ReviewWeekResponse.Min(lastDay, today));
        ReviewStreakResponse? longest = null;
        var length = 0;
        foreach (var week in TrainingWeek.Range(TrainingWeek.Of(firstDay), lastWeek))
        {
            length = weeksWithRuns.Contains(week) ? length + 1 : 0;
            if (length > 0 && length >= (longest?.Weeks ?? 0))
                longest = new ReviewStreakResponse(
                    length, ReviewWeekResponse.Max(week.WeeksBefore(length - 1).Start, firstDay), ReviewWeekResponse.Min(week.End, lastDay));
        }

        return longest;
    }
}

/// <param name="Distance">Metres.</param>
/// <param name="TargetTime">Seconds.</param>
/// <param name="RaceResult">None without a Run on race day.</param>
internal sealed record ReviewGoalRaceResponse(int Id, string Name, DateOnly Date, double Distance, int? TargetTime, RaceResultResponse? RaceResult)
{
    public static ReviewGoalRaceResponse From(GoalRace race, RaceResult? result) => new(
        race.Id, race.Name, race.Date, race.Distance, race.TargetTime, result is null ? null : RaceResultResponse.From(result, race.TargetTime));
}

/// <param name="Distance">Metres, in the year's Runs.</param>
internal sealed record ReviewShoeResponse(string Name, int Runs, double Distance)
{
    /// <summary>Of the Shoes Strava still lists.</summary>
    public static ReviewShoeResponse? MostUsed(IReadOnlyCollection<ReviewRun> runs, IReadOnlyDictionary<string, string> shoeNames) =>
        runs
            .Where(r => r.ShoeId is not null && shoeNames.ContainsKey(r.ShoeId))
            .GroupBy(r => r.ShoeId!)
            .Select(g => new ReviewShoeResponse(shoeNames[g.Key], g.Count(), g.Sum(r => r.Distance)))
            .OrderByDescending(s => s.Distance)
            .FirstOrDefault();
}
