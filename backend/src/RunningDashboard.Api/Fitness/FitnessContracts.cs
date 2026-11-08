namespace RunningDashboard.Api.Fitness;

/// <param name="Runs">Every Fitness Run, oldest first.</param>
/// <param name="Months">Every month with a Fitness Run, oldest first.</param>
internal sealed record FitnessResponse(IReadOnlyList<FitnessRunResponse> Runs, IReadOnlyList<FitnessMonthResponse> Months);

/// <param name="Distance">Metres.</param>
/// <param name="Cadence">Steps per minute, both feet together.</param>
internal sealed record FitnessRunResponse(long Id, string Name, DateTime StartLocal, double Distance, double? Cadence);

/// <summary>The average Cadence of a month's Fitness Runs that have one; none where none has it.</summary>
/// <param name="Month">1 for January.</param>
internal sealed record FitnessMonthResponse(int Year, int Month, double? Cadence)
{
    public static List<FitnessMonthResponse> Of(IEnumerable<FitnessRunResponse> runs) =>
        [.. runs
            .GroupBy(r => (r.StartLocal.Year, r.StartLocal.Month))
            .OrderBy(g => g.Key)
            .Select(g => new FitnessMonthResponse(g.Key.Year, g.Key.Month, Average(g.Select(r => r.Cadence))))];

    private static double? Average(IEnumerable<double?> values)
    {
        var present = values.OfType<double>().ToList();
        return present.Count > 0 ? present.Average() : null;
    }
}

/// <summary>Fitness &amp; Freshness over the whole history, and the Goal Races to mark on it.</summary>
/// <param name="Days">Every day from the first Run's up to today, oldest first; empty without Runs.</param>
/// <param name="Races">Every Goal Race, by date.</param>
internal sealed record FreshnessResponse(IReadOnlyList<FreshnessDayResponse> Days, IReadOnlyList<FreshnessRaceResponse> Races);

/// <summary>One day of Fitness &amp; Freshness, each figure to one decimal.</summary>
/// <param name="Load">The day's Training Load: the Relative Effort of its Runs, summed.</param>
/// <param name="RunId">The day's hardest Run, by Relative Effort; none on a day without a Run.</param>
/// <param name="RunName">Its name.</param>
internal sealed record FreshnessDayResponse(DateOnly Date, double Load, double Fitness, double Fatigue, double Form, long? RunId, string? RunName)
{
    public static FreshnessDayResponse From(FreshnessDay day, (long Id, string Name)? hardestRun) =>
        new(day.Date, Round(day.Load), Round(day.Fitness), Round(day.Fatigue), Round(day.Form), hardestRun?.Id, hardestRun?.Name);

    private static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}

internal sealed record FreshnessRaceResponse(DateOnly Date, string Name);
