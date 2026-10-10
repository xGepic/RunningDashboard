using System.Text.Json;
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

    /// <param name="Length">12, 16 or 20 Training Weeks.</param>
    /// <param name="Phases">The type of each 4-week Phase, first to last: "build", "peak" or "taper".</param>
    private sealed record TrainingPlanRequest(int? Length, string[]? Phases)
    {
        /// <summary>Errors keyed by field, as the form names them; empty if the plan can be saved.</summary>
        public Dictionary<string, string[]> Validate()
        {
            if (Phases is not null && PhaseTypes() is null)
                return new() { ["phases"] = ["Each Phase is a Build, Peak or Taper."] };
            return PlanningRules.TrainingPlanErrors(Length, PhaseTypes());
        }

        /// <summary>The Phases by name; null if one is not "build", "peak" or "taper".</summary>
        public List<PhaseType>? PhaseTypes()
        {
            var types = new List<PhaseType>();
            foreach (var name in Phases ?? [])
            {
                // Names only: Enum.TryParse would also take "7".
                if (!Enum.GetNames<PhaseType>().Contains(name, StringComparer.OrdinalIgnoreCase))
                    return null;
                types.Add(Enum.Parse<PhaseType>(name, ignoreCase: true));
            }

            return Phases is null ? null : types;
        }
    }

    /// <param name="Length">Training Weeks, including the Race Week.</param>
    /// <param name="Phases">First to last.</param>
    /// <param name="Weeks">First week to Race Week.</param>
    private sealed record TrainingPlanResponse(int GoalRaceId, int Length, IReadOnlyList<PhaseResponse> Phases, IReadOnlyList<WeekResponse> Weeks)
    {
        public static TrainingPlanResponse From(GoalRace race)
        {
            var plan = race.TrainingPlan!;
            var block = race.TrainingBlock;
            return new TrainingPlanResponse(
                race.Id,
                plan.Length,
                plan.Phases
                    .Select((type, i) => (Type: type, Weeks: plan.WeeksOfPhase(i)))
                    .Select(p => new PhaseResponse(ApiName(p.Type), p.Weeks.First, p.Weeks.Last))
                    .ToList(),
                plan.Weeks
                    .OrderBy(w => w.WeeksToRace)
                    .Select(w => (w.WeeksToRace, Week: block.WeekAt(w.WeeksToRace)))
                    .Select(w => new WeekResponse(w.WeeksToRace, w.Week.Start, w.Week.End, ApiName(plan.PhaseOf(w.WeeksToRace))))
                    .ToList());
        }

        /// <summary>"build", "peak" or "taper".</summary>
        private static string ApiName(PhaseType type) => JsonNamingPolicy.CamelCase.ConvertName(type.ToString());
    }

    /// <param name="Type">"build", "peak" or "taper".</param>
    private sealed record PhaseResponse(string Type, int FirstWeeksToRace, int LastWeeksToRace);

    /// <param name="Start">The Monday.</param>
    /// <param name="End">The Sunday.</param>
    /// <param name="Phase">The type of the Phase the week is in.</param>
    private sealed record WeekResponse(int WeeksToRace, DateOnly Start, DateOnly End, string Phase);
}
