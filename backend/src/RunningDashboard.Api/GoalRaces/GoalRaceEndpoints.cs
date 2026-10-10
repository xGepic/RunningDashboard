using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Training;

namespace RunningDashboard.Api.GoalRaces;

public static class GoalRaceEndpoints
{
    public static void MapGoalRaceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/goal-races", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var races = await db.GoalRaces.ToListAsync(cancellationToken);
            return races.OrderByDescending(r => r.Date).Select(GoalRaceResponse.From).ToList();
        });

        app.MapPost("/api/goal-races", async (GoalRaceRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (await RejectAsync(request, null, db, cancellationToken) is { } rejected)
                return rejected;

            var race = request.ToGoalRace();
            db.GoalRaces.Add(race);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/goal-races/{race.Id}", GoalRaceResponse.From(race));
        });

        app.MapPut("/api/goal-races/{id:int}", async (int id, GoalRaceRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var race = await db.GoalRaces.FindAsync([id], cancellationToken);
            if (race is null)
                return Results.NotFound();
            if (await RejectAsync(request, id, db, cancellationToken) is { } rejected)
                return rejected;

            request.ApplyTo(race);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(GoalRaceResponse.From(race));
        });

        app.MapDelete("/api/goal-races/{id:int}", async (int id, AppDbContext db, CancellationToken cancellationToken) =>
            await db.GoalRaces.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken) == 0
                ? Results.NotFound()
                : Results.NoContent());
    }

    /// <summary>
    /// Problem details for invalid fields, or for a Training Block overlapping that of a Goal Race other than
    /// <paramref name="id"/>; null if the race can be saved.
    /// </summary>
    private static async Task<IResult?> RejectAsync(GoalRaceRequest request, int? id, AppDbContext db, CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var race = request.ToGoalRace();
        var otherRaces = await db.GoalRaces.Where(r => r.Id != id).ToListAsync(cancellationToken);
        if (PlanningRules.OverlappingRace(race, otherRaces) is { } overlapping)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["trainingBlockWeeks"] = [$"The Training Block ({race.TrainingBlock}) overlaps the one of {overlapping.Name} ({overlapping.TrainingBlock})."],
            });
        }

        return null;
    }

    /// <param name="Distance">Metres.</param>
    /// <param name="TargetTime">Seconds; none for a race without a Target Time.</param>
    /// <param name="TrainingBlockWeeks">Training Weeks in the block, including the Race Week.</param>
    private sealed record GoalRaceRequest(string? Name, DateOnly? Date, double? Distance, int? TargetTime, int? TrainingBlockWeeks)
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
    private sealed record GoalRaceResponse(
        int Id,
        string Name,
        DateOnly Date,
        double Distance,
        int? TargetTime,
        int TrainingBlockWeeks,
        DateOnly TrainingBlockStart,
        DateOnly TrainingBlockEnd)
    {
        public static GoalRaceResponse From(GoalRace race) => new(
            race.Id,
            race.Name,
            race.Date,
            race.Distance,
            race.TargetTime,
            race.TrainingBlockWeeks,
            race.TrainingBlock.Start,
            race.TrainingBlock.End);
    }
}
