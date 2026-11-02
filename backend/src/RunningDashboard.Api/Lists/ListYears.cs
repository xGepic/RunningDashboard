namespace RunningDashboard.Api.Lists;

/// <summary>The years a history list can be narrowed to.</summary>
public static class ListYears
{
    /// <summary>The years of the items' local starts, each once, newest first.</summary>
    public static List<int> Of<T>(IEnumerable<T> items, Func<T, DateTime> startLocal) =>
        [.. items.Select(i => startLocal(i).Year).Distinct().OrderDescending()];
}
