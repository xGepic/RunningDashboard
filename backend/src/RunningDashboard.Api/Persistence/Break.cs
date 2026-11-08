namespace RunningDashboard.Api.Persistence;

/// <summary>
/// Days the Athlete marked as a Break, e.g. for an injury or a holiday. A Break only explains: totals, Training Weeks,
/// the Streak and goals count as they would without it. Two Breaks never overlap.
/// </summary>
public sealed class Break
{
    public const int MaxNoteLength = 200;

    public int Id { get; set; }

    public BreakKind Kind { get; set; }

    /// <summary>The first day of the Break.</summary>
    public DateOnly Start { get; set; }

    /// <summary>The last day of the Break, which may be <see cref="Start"/>.</summary>
    public DateOnly End { get; set; }

    /// <summary>What it was, in the Athlete's words, e.g. "Calf strain"; none if they gave none.</summary>
    public string? Note { get; set; }
}
