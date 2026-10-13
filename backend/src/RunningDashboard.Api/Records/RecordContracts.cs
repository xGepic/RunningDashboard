using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Records;

/// <param name="Name">Strava's name for the distance, e.g. "5K" or "Half-Marathon".</param>
/// <param name="Distance">Metres.</param>
/// <param name="ShownByDefault">Whether the Records page shows this distance before the Athlete picks any.</param>
/// <param name="Progression">Each Best Effort that improved the Personal Best, oldest first.</param>
internal sealed record RecordDistanceResponse(
    string Name,
    double Distance,
    bool ShownByDefault,
    IReadOnlyList<PersonalBestResponse> Progression,
    PersonalBestResponse PersonalBest);

/// <param name="Date">Local start of the Run.</param>
/// <param name="ElapsedTime">Seconds.</param>
/// <param name="TimePerKm">Elapsed time per kilometre, in seconds.</param>
internal sealed record PersonalBestResponse(long RunId, string RunName, DateTime Date, int ElapsedTime, double? TimePerKm)
{
    public static PersonalBestResponse From(RunBestEffort effort) =>
        new(effort.RunId, effort.RunName, effort.StartLocal, effort.ElapsedTime, Run.PerKilometre(effort.ElapsedTime, effort.Distance));
}
