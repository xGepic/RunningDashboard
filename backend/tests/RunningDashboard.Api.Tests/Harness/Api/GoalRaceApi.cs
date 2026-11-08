using System.Net;
using System.Net.Http.Json;

namespace RunningDashboard.Api.Tests.Harness;

// The JSON of /api/goal-races, as the tests read and write it.

/// <param name="Distance">Metres.</param>
/// <param name="TargetTime">Seconds.</param>
public sealed record GoalRaceRequest(string? Name, DateOnly? Date, double? Distance, int? TargetTime, int? TrainingBlockWeeks);

/// <param name="TrainingPlanLength">None without a Training Plan.</param>
/// <param name="RaceResult">None before race day.</param>
public sealed record GoalRaceResponse(
    int Id,
    string Name,
    DateOnly Date,
    double Distance,
    int? TargetTime,
    int TrainingBlockWeeks,
    DateOnly TrainingBlockStart,
    DateOnly TrainingBlockEnd,
    int? TrainingPlanLength,
    RaceResultResponse? RaceResult);

/// <param name="ElapsedTime">Seconds.</param>
/// <param name="Source">"bestEffort", "elapsedTime" or "pending".</param>
/// <param name="DifferenceFromTarget">Seconds over (positive) or under (negative) the Target Time.</param>
public sealed record RaceResultResponse(long RunId, string RunName, int? ElapsedTime, string Source, int? DifferenceFromTarget);

public static class GoalRaceApi
{
    /// <summary>Adds the Goal Race; fails the test unless the app accepts it.</summary>
    public static async Task<GoalRaceResponse> CreateGoalRaceAsync(this HttpClient browser, GoalRaceRequest race)
    {
        var response = await browser.PostAsJsonAsync("/api/goal-races", race);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<GoalRaceResponse>())!;
    }

    public static async Task<List<GoalRaceResponse>> ListGoalRacesAsync(this HttpClient browser) =>
        (await browser.GetFromJsonAsync<List<GoalRaceResponse>>("/api/goal-races"))!;
}
