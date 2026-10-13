namespace RunningDashboard.Api.Persistence;

/// <summary>
/// How the Athlete wants the dashboard shown, and the Shoes' Maximum Distance. There is at most one row; without it,
/// the defaults apply.
/// </summary>
public sealed class AthleteSettings
{
    public const int SingletonId = 1;

    public const string Kilometres = "km";
    public const string Miles = "mi";

    public int Id { get; set; } = SingletonId;

    /// <summary>The Distance Unit: <see cref="Kilometres"/> or <see cref="Miles"/>. Everything is still stored in metres.</summary>
    public string DistanceUnit { get; set; } = Kilometres;

    /// <summary>Metres after which every Shoe is due to be retired.</summary>
    public double ShoeMaximumDistance { get; set; } = 700_000;

    public static bool IsDistanceUnit(string? unit) => unit is Kilometres or Miles;
}
