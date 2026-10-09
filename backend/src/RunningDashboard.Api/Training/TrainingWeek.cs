namespace RunningDashboard.Api.Training;

/// <summary>
/// A Training Week: Monday to Sunday. A Run belongs to the week of its local start date, so a Run late on Sunday,
/// or one abroad, counts where and when it was run.
/// </summary>
/// <param name="Start">The Monday.</param>
public readonly record struct TrainingWeek(DateOnly Start)
{
    public static TrainingWeek Of(DateOnly date) => new(date.AddDays(-(((int)date.DayOfWeek + 6) % 7)));

    public static TrainingWeek Of(DateTime startLocal) => Of(DateOnly.FromDateTime(startLocal));

    public TrainingWeek Next => new(Start.AddDays(7));

    /// <summary>Every Training Week from <paramref name="first"/> through <paramref name="last"/>.</summary>
    public static IEnumerable<TrainingWeek> Range(TrainingWeek first, TrainingWeek last)
    {
        for (var week = first; week.Start <= last.Start; week = week.Next)
            yield return week;
    }
}
