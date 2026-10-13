using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;
using RunningDashboard.Api.Training;

namespace RunningDashboard.Api.GoalRaces;

public static class TrainingPlanEndpoints
{
    public static void MapTrainingPlanEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/goal-races/{id:int}/training-plan", async (int id, AppDbContext db, CancellationToken cancellationToken) =>
            await db.GoalRaces.FindAsync([id], cancellationToken) is { TrainingPlan: not null } race
                ? Results.Ok(TrainingPlanResponse.From(race))
                : Results.NotFound());

        // Creates the Goal Race's Training Plan, or reshapes the one it has.
        app.MapPut("/api/goal-races/{id:int}/training-plan", async (int id, TrainingPlanRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var race = await db.GoalRaces.FindAsync([id], cancellationToken);
            if (race is null)
                return Results.NotFound();

            var errors = request.Validate();
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var block = TrainingBlock.Ending(race.Date, request.Length!.Value);
            var otherRaces = await db.GoalRaces.Where(r => r.Id != id).ToListAsync(cancellationToken);
            if (PlanningRules.OverlapError(block, otherRaces) is { } overlap)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["length"] = [overlap] });

            race.TrainingPlan ??= new TrainingPlan();
            race.TrainingPlan.Shape(request.Length.Value, request.PhaseTypes()!);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(TrainingPlanResponse.From(race));
        });

        // Plans the weeks in the request; the plan's other weeks keep what they had.
        app.MapPut("/api/goal-races/{id:int}/training-plan/weeks", async (int id, TrainingPlanWeeksRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var race = await db.GoalRaces.FindAsync([id], cancellationToken);
            if (race?.TrainingPlan is not { } plan)
                return Results.NotFound();

            var errors = request.Validate(plan);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            foreach (var week in request.Weeks ?? [])
                week.ApplyTo(plan.Weeks.Single(w => w.WeeksToRace == week.WeeksToRace));
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(TrainingPlanResponse.From(race));
        });

        // Deletes the plan with all its weeks; the race's own Training Block length counts again.
        app.MapDelete("/api/goal-races/{id:int}/training-plan", async (int id, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var race = await db.GoalRaces.FindAsync([id], cancellationToken);
            if (race?.TrainingPlan is null)
                return Results.NotFound();

            race.TrainingPlan = null;
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
    }
}
