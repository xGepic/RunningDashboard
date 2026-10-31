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
