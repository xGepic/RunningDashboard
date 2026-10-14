using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Fitness;

/// <summary>One km Split as the fitness figures need it.</summary>
/// <param name="Distance">Metres.</param>
/// <param name="MovingTime">Seconds.</param>
/// <param name="HeartRate">Beats per minute.</param>
/// <param name="GradeAdjustedSpeed">Metres per second.</param>
public sealed record FitnessSplit(int Index, double Distance, int MovingTime, double? HeartRate, double? GradeAdjustedSpeed);

/// <summary>
/// Aerobic Efficiency and Cardiac Drift, from a Run's km Splits. Pure calculations: which Runs get them (Fitness Runs,
/// Long Runs) is up to the caller, through <see cref="IsFitnessRun"/> and <see cref="HasCardiacDrift"/>.
/// </summary>
public static class FitnessFigures
{
    /// <summary>Metres; a shorter Long Run gets no Cardiac Drift, as its drift is noise.</summary>
    public const double CardiacDriftMinimumDistance = 10_000;

    /// <summary>Up to this much Cardiac Drift is good.</summary>
    public const double GoodCardiacDrift = 0.05;

    /// <summary>Not on a treadmill or virtual, whose Pace is unreliable, and not on a Goal Race's race day.</summary>
    public static bool IsFitnessRun(bool isTreadmill, DateTime startLocal, IReadOnlySet<DateOnly> raceDays) =>
        !isTreadmill && !raceDays.Contains(DateOnly.FromDateTime(startLocal));

    /// <summary>Whether a Fitness Run gets a Cardiac Drift: as its week's Long Run, at least 10 km long.</summary>
    public static bool HasCardiacDrift(bool isLongRun, double distance) => isLongRun && distance >= CardiacDriftMinimumDistance;

    /// <summary>
    /// Metres per heartbeat: grade-adjusted distance per minute over the Splits, divided by the Run's average heart
    /// rate. None without heart rate, or when a Split lacks its grade-adjusted speed.
    /// </summary>
    public static double? AerobicEfficiency(double? averageHeartRate, IReadOnlyCollection<FitnessSplit> splits)
    {
        var movingTime = splits.Sum(s => s.MovingTime);
        if (averageHeartRate is not > 0 || movingTime == 0 || splits.Any(s => s.GradeAdjustedSpeed is null))
            return null;

        var gradeAdjustedDistance = splits.Sum(s => s.GradeAdjustedSpeed!.Value * s.MovingTime);
        return gradeAdjustedDistance / movingTime * 60 / averageHeartRate.Value;
    }

    /// <summary>
    /// How much heart rate per grade-adjusted speed rose from the first half to the second, as a fraction (0.05 is 5%).
    /// The halves are split by distance: a Split straddling the middle counts in both, in proportion. None when a
    /// Split lacks its heart rate or grade-adjusted speed.
    /// </summary>
    public static double? CardiacDrift(IReadOnlyCollection<FitnessSplit> splits)
    {
        if (splits.Count == 0 || splits.Any(s => s.HeartRate is null || s.GradeAdjustedSpeed is null))
            return null;

        var middle = splits.Sum(s => s.Distance) / 2;
        // Per half: heartbeats and grade-adjusted metres, each Split's averages weighted by its Moving Time.
        double beats1 = 0, metres1 = 0, beats2 = 0, metres2 = 0;
        double before = 0;
        foreach (var split in splits.OrderBy(s => s.Index))
        {
            var inFirstHalf = split.Distance > 0 ? Math.Clamp((middle - before) / split.Distance, 0, 1) : before < middle ? 1 : 0;
            var beats = split.HeartRate!.Value * split.MovingTime;
            var metres = split.GradeAdjustedSpeed!.Value * split.MovingTime;
            beats1 += inFirstHalf * beats;
            metres1 += inFirstHalf * metres;
            beats2 += (1 - inFirstHalf) * beats;
            metres2 += (1 - inFirstHalf) * metres;
            before += split.Distance;
        }

        if (metres1 <= 0 || metres2 <= 0 || beats1 <= 0)
            return null;
        return beats2 / metres2 / (beats1 / metres1) - 1;
    }

    public static FitnessSplit From(Split split) => new(split.Index, split.Distance, split.MovingTime, split.HeartRate, split.GradeAdjustedSpeed);
}
