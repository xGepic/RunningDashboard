namespace RunningDashboard.Api.Fitness;

/// <summary>One km Split as the fitness figures need it.</summary>
/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="HeartRate">Beats per minute.</param>
/// <param name="GradeAdjustedSpeed">Metres per second.</param>
public sealed record FitnessSplit(int Index, double Distance, int MovingTime, double? HeartRate, double? GradeAdjustedSpeed);

/// <summary>One day of Fitness &amp; Freshness.</summary>
/// <param name="Load">The day's Training Load: the Relative Effort of its Runs, summed.</param>
/// <param name="Fitness">The Training Load averaged over about the last six weeks, recent days weighing more.</param>
/// <param name="Fatigue">The same over about the last week.</param>
public sealed record FreshnessDay(DateOnly Date, double Load, double Fitness, double Fatigue)
{
    /// <summary>Fitness minus Fatigue: above zero the last week was lighter than the weeks before, below it harder.</summary>
    public double Form => Fitness - Fatigue;
}
