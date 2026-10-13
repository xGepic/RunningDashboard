using System.Text.Json;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.GoalRaces;

/// <param name="Distance">Metres.</param>
/// <param name="TargetTime">Seconds; none for a race without a Target Time.</param>
/// <param name="TrainingBlockWeeks">Training Weeks in the block, including the Race Week.</param>
internal sealed record GoalRaceRequest(string? Name, DateOnly? Date, double? Distance, int? TargetTime, int? TrainingBlockWeeks)
{
    private const int MaxNameLength = 200;

    /// <summary>Two years: longer than any real block.</summary>
    private const int MaxTrainingBlockWeeks = 104;

    /// <summary>Far beyond any running career on Strava, and keeps every Training Block's dates valid.</summary>
    private static readonly DateOnly EarliestDate = new(1900, 1, 1);

    private static readonly DateOnly LatestDate = new(2099, 12, 31);

    /// <summary>Errors keyed by field, as the form names them; empty if every field is valid.</summary>
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Name))
            errors["name"] = ["Give the race a name."];
        else if (Name.Trim().Length > MaxNameLength)
            errors["name"] = [$"The name is at most {MaxNameLength} characters."];
        if (Date is null)
            errors["date"] = ["Give the race date."];
        else if (Date < EarliestDate || Date > LatestDate)
            errors["date"] = [$"The race date is between {EarliestDate.Year} and {LatestDate.Year}."];
        if (Distance is not > 0)
            errors["distance"] = ["The distance is a positive number of metres."];
        if (TargetTime is <= 0)
            errors["targetTime"] = ["The Target Time is a positive number of seconds, or none."];
        if (TrainingBlockWeeks is not (> 0 and <= MaxTrainingBlockWeeks))
            errors["trainingBlockWeeks"] = [$"The Training Block is 1 to {MaxTrainingBlockWeeks} weeks long."];
        return errors;
    }

    /// <summary>A new Goal Race from a request that passed <see cref="Validate"/>.</summary>
    public GoalRace ToGoalRace()
    {
        var race = new GoalRace { Name = Name!.Trim() };
        ApplyTo(race);
        return race;
    }

    public void ApplyTo(GoalRace race)
    {
        race.Name = Name!.Trim();
        race.Date = Date!.Value;
        race.Distance = Distance!.Value;
        race.TargetTime = TargetTime;
        race.TrainingBlockWeeks = TrainingBlockWeeks!.Value;
    }
}

/// <param name="Distance">Metres.</param>
/// <param name="TargetTime">Seconds.</param>
/// <param name="TrainingBlockStart">The Monday the Training Block starts.</param>
/// <param name="TrainingBlockEnd">The Sunday of the Race Week, when the Training Block ends.</param>
/// <param name="TrainingPlanLength">The Training Plan's length, which is the block's; none without a plan.</param>
/// <param name="RaceResult">None without a Run on race day.</param>
internal sealed record GoalRaceResponse(
    int Id,
    string Name,
    DateOnly Date,
    double Distance,
    int? TargetTime,
    int TrainingBlockWeeks,
    DateOnly TrainingBlockStart,
    DateOnly TrainingBlockEnd,
    int? TrainingPlanLength,
    RaceResultResponse? RaceResult)
{
    public static GoalRaceResponse From(GoalRace race, RaceResult? result) => new(
        race.Id,
        race.Name,
        race.Date,
        race.Distance,
        race.TargetTime,
        race.TrainingBlockWeeks,
        race.TrainingBlock.Start,
        race.TrainingBlock.End,
        race.TrainingPlan?.Length,
        result is null ? null : RaceResultResponse.From(result, race.TargetTime));
}

/// <param name="ElapsedTime">Seconds; none while pending.</param>
/// <param name="Source">
/// "bestEffort": the Run's Best Effort over the race distance; "elapsedTime": the whole Run's, as it has none;
/// "pending": the Run's Best Efforts are still to be fetched.
/// </param>
/// <param name="DifferenceFromTarget">Seconds over (positive) or under (negative) the Target Time; none without one.</param>
internal sealed record RaceResultResponse(long RunId, string RunName, int? ElapsedTime, string Source, int? DifferenceFromTarget)
{
    public static RaceResultResponse From(RaceResult result, int? targetTime) => new(
        result.RunId,
        result.RunName,
        result.ElapsedTime,
        JsonNamingPolicy.CamelCase.ConvertName(result.Source.ToString()),
        result.ElapsedTime - targetTime);
}
