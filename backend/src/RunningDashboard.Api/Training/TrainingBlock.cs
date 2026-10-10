using System.Globalization;

namespace RunningDashboard.Api.Training;

/// <summary>
/// The Training Weeks that lead up to a Goal Race and end with its Race Week.
/// </summary>
/// <param name="FirstWeek">The block's first Training Week.</param>
/// <param name="RaceWeek">The Training Week of the Goal Race (Weeks to Race 0).</param>
public readonly record struct TrainingBlock(TrainingWeek FirstWeek, TrainingWeek RaceWeek)
{
    /// <summary>The block of <paramref name="weeks"/> Training Weeks ending with the week of <paramref name="raceDate"/>.</summary>
    public static TrainingBlock Ending(DateOnly raceDate, int weeks)
    {
        var raceWeek = TrainingWeek.Of(raceDate);
        return new TrainingBlock(new TrainingWeek(raceWeek.Start.AddDays(-7 * (weeks - 1))), raceWeek);
    }

    /// <summary>The Monday of the first week.</summary>
    public DateOnly Start => FirstWeek.Start;

    /// <summary>The Sunday of the Race Week.</summary>
    public DateOnly End => RaceWeek.Start.AddDays(6);

    public bool Overlaps(TrainingBlock other) => Start <= other.End && other.Start <= End;

    /// <summary>"22 Jun – 27 Sep 2026", with both years when the block spans New Year.</summary>
    public override string ToString()
    {
        var start = Start.ToString(Start.Year == End.Year ? "d MMM" : "d MMM yyyy", CultureInfo.InvariantCulture);
        return $"{start} – {End.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";
    }
}
