namespace RunningDashboard.Api.Persistence;

/// <summary>The purpose of a Phase: 4 consecutive Training Weeks of a Training Plan.</summary>
public enum PhaseType
{
    Build,
    Peak,
    Taper,
}

/// <summary>What a Running Day is planned for. Runs themselves never have one.</summary>
public enum RunType
{
    Easy,
    Long,
    Tempo,
    Intervals,
    Race,
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

    /// <summary>The days the Athlete plans to run on, at most one per weekday, Monday first; the other days are rest days.</summary>
    public List<RunningDay> RunningDays { get; set; } = [];

    /// <summary>A week of deliberately lower load; never the Race Week.</summary>
    public bool RecoveryWeek { get; set; }
}

/// <summary>A day of a plan week the Athlete plans to run on, and what for.</summary>
public sealed record RunningDay(DayOfWeek Day, RunType RunType);
