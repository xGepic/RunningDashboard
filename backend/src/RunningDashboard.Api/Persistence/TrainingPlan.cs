namespace RunningDashboard.Api.Persistence;

/// <summary>
/// What the Athlete intends to do during a Goal Race's Training Block: its Phases, and its weeks. The weeks are tied
/// to Weeks to Race, not to dates, so moving the Goal Race moves the plan.
/// </summary>
public sealed class TrainingPlan
{
    /// <summary>Training Weeks in a Phase.</summary>
    public const int PhaseWeeks = 4;

    /// <summary>The lengths a plan can have, in Training Weeks.</summary>
    public static readonly IReadOnlyList<int> Lengths = [12, 16, 20];

    /// <summary>Training Weeks, including the Race Week; it is the Training Block's length.</summary>
    public int Length { get; private set; }

    /// <summary>The type of each Phase, first to last.</summary>
    public List<PhaseType> Phases { get; private set; } = [];

    /// <summary>One per Weeks to Race from −(Length−1) to 0, in no particular order.</summary>
    public List<TrainingPlanWeek> Weeks { get; private set; } = [];

    /// <summary>
    /// Sets the length and the Phases. Weeks still within the new length keep what was planned for them, weeks before
    /// it are dropped, and new weeks start empty, pre-marked as Recovery Weeks where <see cref="IsDefaultRecoveryWeek"/>.
    /// </summary>
    public void Shape(int length, IReadOnlyList<PhaseType> phases)
    {
        Length = length;
        Phases = [.. phases];
        Weeks.RemoveAll(w => w.WeeksToRace <= -length);
        var planned = Weeks.Select(w => w.WeeksToRace).ToHashSet();
        Weeks.AddRange(Enumerable.Range(1 - length, length)
            .Where(n => !planned.Contains(n))
            .Select(n => new TrainingPlanWeek { WeeksToRace = n, RecoveryWeek = IsDefaultRecoveryWeek(n) }));
    }

    /// <summary>
    /// Whether a new week starts as a Recovery Week: in a plan whose length divides into 4-week stretches, every 4th
    /// week from the first, except the Race Week.
    /// </summary>
    private bool IsDefaultRecoveryWeek(int weeksToRace) =>
        Length % PhaseWeeks == 0 && weeksToRace != 0 && (weeksToRace + Length) % PhaseWeeks == 0;

    /// <summary>The Phase the week <paramref name="weeksToRace"/> falls in; Phases follow each other from the plan's first week.</summary>
    public PhaseType PhaseOf(int weeksToRace) => Phases[(weeksToRace + Length - 1) / PhaseWeeks];

    /// <summary>Each Phase, first to last, with its first and last Weeks to Race.</summary>
    public IEnumerable<(PhaseType Type, int FirstWeeksToRace, int LastWeeksToRace)> PhaseSpans() =>
        Phases.Select((type, i) =>
        {
            var first = 1 - Length + i * PhaseWeeks;
            return (type, first, first + PhaseWeeks - 1);
        });
}
