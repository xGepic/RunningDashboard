namespace RunningDashboard.Api.Fitness;

/// <summary>
/// Fitness &amp; Freshness: Fitness, Fatigue and Form day by day, from each day's Training Load. Fitness and Fatigue
/// are exponentially weighted averages of the daily load with time constants of 42 and 7 days (Banister's
/// impulse-response model, as Strava's chart of the same name): each day moves them a 42nd and a 7th of the way
/// towards that day's load. Both start at zero on the first day with a Run.
/// </summary>
public static class FitnessFreshness
{
    public const int FitnessDays = 42;

    public const int FatigueDays = 7;

    /// <summary>
    /// Every day from the first with a load up to <paramref name="today"/> (or the last with a load, if later), days
    /// without a Run at zero load; none without any day.
    /// </summary>
    public static IReadOnlyList<FreshnessDay> Of(IReadOnlyDictionary<DateOnly, double> loadByDay, DateOnly today)
    {
        if (loadByDay.Count == 0)
            return [];

        var first = loadByDay.Keys.Min();
        var last = loadByDay.Keys.Max() is var latest && latest > today ? latest : today;
        var days = new List<FreshnessDay>(last.DayNumber - first.DayNumber + 1);
        double fitness = 0, fatigue = 0;
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var load = loadByDay.GetValueOrDefault(day);
            fitness += (load - fitness) / FitnessDays;
            fatigue += (load - fatigue) / FatigueDays;
            days.Add(new FreshnessDay(day, load, fitness, fatigue));
        }

        return days;
    }
}
