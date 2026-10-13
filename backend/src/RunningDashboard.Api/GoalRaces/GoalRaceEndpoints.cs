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
            return await ResponsesAsync([.. races.OrderByDescending(r => r.Date)], db, cancellationToken);
        });

        app.MapPost("/api/goal-races", async (GoalRaceRequest request, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            if (await RejectAsync(request, null, db, clock, cancellationToken) is { } rejected)
                return rejected;

            var race = request.ToGoalRace();
            db.GoalRaces.Add(race);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/goal-races/{race.Id}", (await ResponsesAsync([race], db, cancellationToken)).Single());
        });

        app.MapPut("/api/goal-races/{id:int}", async (int id, GoalRaceRequest request, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var race = await db.GoalRaces.FindAsync([id], cancellationToken);
            if (race is null)
                return Results.NotFound();
            if (await RejectAsync(request, race, db, clock, cancellationToken) is { } rejected)
                return rejected;

            request.ApplyTo(race);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok((await ResponsesAsync([race], db, cancellationToken)).Single());
        });

        app.MapDelete("/api/goal-races/{id:int}", async (int id, AppDbContext db, CancellationToken cancellationToken) =>
            await db.GoalRaces.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken) == 0
                ? Results.NotFound()
                : Results.NoContent());
    }

    /// <summary>The <paramref name="races"/> with their Race Results, in the same order.</summary>
    private static async Task<List<GoalRaceResponse>> ResponsesAsync(IReadOnlyCollection<GoalRace> races, AppDbContext db, CancellationToken cancellationToken)
    {
        var results = await RaceResults.OfAsync(races, db, cancellationToken);
        return [.. races.Select(r => GoalRaceResponse.From(r, results.GetValueOrDefault(r.Id)))];
    }

    /// <summary>
    /// Problem details for invalid fields, for a second upcoming Goal Race, or for a Training Block overlapping that of
    /// another Goal Race; null if the race can be saved. An <paramref name="existing"/> race with a Training Plan keeps
    /// the plan's length.
    /// </summary>
    private static async Task<IResult?> RejectAsync(GoalRaceRequest request, GoalRace? existing, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var block = TrainingBlock.Ending(request.Date!.Value, existing?.TrainingPlan?.Length ?? request.TrainingBlockWeeks!.Value);
        var excludedId = existing?.Id;
        var otherRaces = await db.GoalRaces.Where(r => r.Id != excludedId).ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        if (PlanningRules.UpcomingRaceError(request.Date!.Value, otherRaces, today) is { } upcoming)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["date"] = [upcoming] });

        return PlanningRules.OverlapError(block, otherRaces) is { } overlap
            ? Results.ValidationProblem(new Dictionary<string, string[]> { ["trainingBlockWeeks"] = [overlap] })
            : null;
    }
}
