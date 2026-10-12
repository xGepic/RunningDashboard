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
        public List<PhaseType>? PhaseTypes() => Phases is null ? null : ApiNames.Parse<PhaseType>(Phases);
    }

    /// <param name="Weeks">The weeks to plan, each at most once.</param>
    private sealed record TrainingPlanWeeksRequest(List<WeekRequest>? Weeks)
    {
        /// <summary>Errors keyed "weeks[i].field", as the editor's rows are numbered; empty if the weeks can be saved.</summary>
        public Dictionary<string, string[]> Validate(TrainingPlan plan)
        {
            var errors = new Dictionary<string, string[]>();
            var seen = new HashSet<int>();
            foreach (var (week, i) in (Weeks ?? []).Select((w, i) => (w, i)))
            {
                foreach (var (field, error) in week.Errors(plan, seen))
                    errors[$"weeks[{i}].{field}"] = [error];
                seen.Add(week.WeeksToRace);
            }

            return errors;
        }
    }

    /// <param name="WeeksToRace">Which week of the plan: 0 for the Race Week.</param>
    /// <param name="PlannedDistance">Metres, or none.</param>
    /// <param name="PlannedLongRun">Metres, or none.</param>
    /// <param name="RunningDays">In any order; the days left out are rest days.</param>
    private sealed record WeekRequest(int WeeksToRace, double? PlannedDistance, double? PlannedLongRun, RunningDayRequest[]? RunningDays, bool RecoveryWeek)
    {
        /// <summary>What is wrong with this week, by field; <paramref name="earlier"/> are the weeks before it in the request.</summary>
        public IEnumerable<(string Field, string Error)> Errors(TrainingPlan plan, HashSet<int> earlier)
        {
            if (WeeksToRace > 0 || WeeksToRace <= -plan.Length)
                yield return ("weeksToRace", $"A {plan.Length}-week plan has weeks {1 - plan.Length} to 0.");
            else if (earlier.Contains(WeeksToRace))
                yield return ("weeksToRace", $"Week {WeeksToRace} is planned twice.");
            if (PlannedDistance <= 0)
                yield return ("plannedDistance", "The planned distance is a positive number of metres, or none.");
            if (PlannedLongRun <= 0)
                yield return ("plannedLongRun", "The planned Long Run is a positive number of metres, or none.");
            if (Days() is not { } days)
                yield return ("runningDays", "Each Running Day is a day of the week, \"monday\" to \"sunday\", with a Run Type: \"easy\", \"long\", \"tempo\", \"intervals\" or \"race\".");
            else if (days.DistinctBy(d => d.Day).Count() < days.Count)
                yield return ("runningDays", "Each day of the week can have only one Run Type.");
            if (PlanningRules.RecoveryWeekError(WeeksToRace, RecoveryWeek) is { } recovery)
                yield return ("recoveryWeek", recovery);
        }

        public void ApplyTo(TrainingPlanWeek week)
        {
            week.PlannedDistance = PlannedDistance;
            week.PlannedLongRun = PlannedLongRun;
            week.RunningDays = [.. Days()!.OrderBy(d => MondayFirst(d.Day))];
            week.RecoveryWeek = RecoveryWeek;
        }

        /// <summary>The Running Days by name; null if one is not a day of the week with a Run Type.</summary>
        private List<RunningDay>? Days()
        {
            var days = new List<RunningDay>();
            foreach (var day in RunningDays ?? [])
            {
                if (ApiNames.Parse<DayOfWeek>(day?.Day) is not { } weekday || ApiNames.Parse<RunType>(day?.RunType) is not { } runType)
                    return null;
                days.Add(new RunningDay(weekday, runType));
            }

            return days;
        }
    }

    /// <param name="Day">"monday" to "sunday".</param>
    /// <param name="RunType">"easy", "long", "tempo", "intervals" or "race".</param>
    private sealed record RunningDayRequest(string? Day, string? RunType);

    /// <summary>Orders days Monday to Sunday, as Training Weeks run.</summary>
    private static int MondayFirst(DayOfWeek day) => ((int)day + 6) % 7;

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
                [.. plan.PhaseSpans().Select(p => new PhaseResponse(ApiNames.Of(p.Type), p.FirstWeeksToRace, p.LastWeeksToRace))],
                [.. plan.Weeks
                    .OrderBy(w => w.WeeksToRace)
                    .Select(w => (Planned: w, Week: block.WeekAt(w.WeeksToRace)))
                    .Select(w => new WeekResponse(
                        w.Planned.WeeksToRace,
                        w.Week.Start,
                        w.Week.End,
                        ApiNames.Of(plan.PhaseOf(w.Planned.WeeksToRace)),
                        w.Planned.PlannedDistance,
                        w.Planned.PlannedLongRun,
                        [.. w.Planned.RunningDays.Select(d => new RunningDayResponse(ApiNames.Of(d.Day), ApiNames.Of(d.RunType)))],
                        w.Planned.RecoveryWeek))]);
        }
    }

    /// <param name="Type">"build", "peak" or "taper".</param>
    private sealed record PhaseResponse(string Type, int FirstWeeksToRace, int LastWeeksToRace);

    /// <param name="Start">The Monday.</param>
    /// <param name="End">The Sunday.</param>
    /// <param name="Phase">The type of the Phase the week is in.</param>
    /// <param name="PlannedDistance">Metres, or none.</param>
    /// <param name="PlannedLongRun">Metres, or none.</param>
    /// <param name="RunningDays">Monday first; the days left out are rest days.</param>
    private sealed record WeekResponse(
        int WeeksToRace,
        DateOnly Start,
        DateOnly End,
        string Phase,
        double? PlannedDistance,
        double? PlannedLongRun,
        IReadOnlyList<RunningDayResponse> RunningDays,
        bool RecoveryWeek);

    /// <param name="Day">"monday" to "sunday".</param>
    /// <param name="RunType">"easy", "long", "tempo", "intervals" or "race".</param>
    private sealed record RunningDayResponse(string Day, string RunType);
}
