namespace RunningDashboard.Api.GoalRaces;

/// <summary>How a Goal Race went, read from the Runs of race day.</summary>
/// <param name="ElapsedTime">Seconds; none while the Run's Best Efforts are still to be fetched.</param>
public sealed record RaceResult(long RunId, string RunName, int? ElapsedTime, RaceResultSource Source);

public enum RaceResultSource
{
    /// <summary>The Run's Best Effort over the Goal Race's distance.</summary>
    BestEffort,

    /// <summary>The Run's elapsed time, as it has no Best Effort over the Goal Race's distance.</summary>
    ElapsedTime,

    /// <summary>The Run's Best Efforts are still to be fetched, so the result is not known yet.</summary>
    Pending,
}
