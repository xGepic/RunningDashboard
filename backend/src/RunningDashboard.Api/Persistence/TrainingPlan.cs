namespace RunningDashboard.Api.Persistence;

/// <summary>The purpose of a Phase: 4 consecutive Training Weeks of a Training Plan.</summary>
public enum PhaseType
{
    Build,
    Peak,
    Taper,
}

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
    /// it are dropped, and new weeks start empty.
    /// </summary>
    public void Shape(int length, IReadOnlyList<PhaseType> phases)
    {
        Length = length;
        Phases = [.. phases];
        Weeks.RemoveAll(w => w.WeeksToRace <= -length);
        var planned = Weeks.Select(w => w.WeeksToRace).ToHashSet();
        Weeks.AddRange(Enumerable.Range(1 - length, length).Where(n => !planned.Contains(n)).Select(n => new TrainingPlanWeek { WeeksToRace = n }));
    }

    /// <summary>The Phase the week <paramref name="weeksToRace"/> falls in; Phases follow each other from the plan's first week.</summary>
    public PhaseType PhaseOf(int weeksToRace) => Phases[(weeksToRace + Length - 1) / PhaseWeeks];

    /// <summary>The first and last Weeks to Race of the Phase at <paramref name="index"/>.</summary>
    public (int First, int Last) WeeksOfPhase(int index)
    {
        var first = 1 - Length + index * PhaseWeeks;
        return (first, first + PhaseWeeks - 1);
    }
}

/// <summary>One Training Week of a Training Plan: its Weekly Target, Running Days and whether it is a Recovery Week.</summary>
public sealed class TrainingPlanWeek
{
    /// <summary>0 for the Race Week, −1 for the week before, and so on.</summary>
    public int WeeksToRace { get; set; }

    /// <summary>Metres; none until the Athlete plans it.</summary>
    public double? PlannedDistance { get; set; }

    /// <summary>The planned Long Run in metres; none until the Athlete plans it.</summary>
    public double? PlannedLongRun { get; set; }

    /// <summary>The days the Athlete plans to run on, Monday first.</summary>
    public List<DayOfWeek> RunningDays { get; set; } = [];

    /// <summary>A week of deliberately lower load; never the Race Week.</summary>
    public bool RecoveryWeek { get; set; }
}
