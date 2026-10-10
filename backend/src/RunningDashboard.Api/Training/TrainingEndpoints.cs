using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Training;

public static class TrainingEndpoints
{
    public static void MapTrainingEndpoints(this IEndpointRouteBuilder app)
    {
        // The Training Block of the given Goal Race, or else the one today falls in.
        app.MapGet("/api/training", async (int? goalRaceId, AppDbContext db, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            GoalRace? race;
            if (goalRaceId is not null)
            {
                race = await db.GoalRaces.FindAsync([goalRaceId], cancellationToken);
                if (race is null)
                    return Results.NotFound();
            }
            else
            {
                race = TrainingCalendar.RaceTrainingFor(await db.GoalRaces.ToListAsync(cancellationToken), today);
            }

            if (race is null)
                return Results.Ok(new TrainingResponse("none", null));

            var block = race.TrainingBlock;
            var from = block.Start.ToDateTime(TimeOnly.MinValue);
            var until = block.End.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var runs = await db.Runs
                .Where(r => r.StartLocal >= from && r.StartLocal < until)
                .Select(r => new RunSummary(r.Id, r.Name, r.StartLocal, r.Distance, r.MovingTime, r.RelativeEffort))
                .ToListAsync(cancellationToken);

            return Results.Ok(new TrainingResponse("block", BlockResponse.From(race, TrainingCalendar.WeeksOf(block, runs, today))));
        });
    }

    /// <param name="View">"block": a Goal Race's Training Block; "none": today is in no Training Block.</param>
    private sealed record TrainingResponse(string View, BlockResponse? Block);

    /// <param name="TargetPace">
    /// Target Time ÷ Goal Race distance, in whole seconds per km rounded down; none without a Target Time.
    /// </param>
    /// <param name="Weeks">First week to Race Week.</param>
    private sealed record BlockResponse(GoalRaceResponse GoalRace, int? TargetPace, IReadOnlyList<WeekResponse> Weeks)
    {
        public static BlockResponse From(GoalRace race, IEnumerable<TrainingBlockWeek> weeks) => new(
            new GoalRaceResponse(race.Id, race.Name, race.Date, race.Distance, race.TargetTime),
            race.TargetPace,
            weeks.Select(w => WeekResponse.From(w, race.TargetPace)).ToList());
    }

    /// <param name="Distance">Metres.</param>
    /// <param name="TargetTime">Seconds.</param>
    private sealed record GoalRaceResponse(int Id, string Name, DateOnly Date, double Distance, int? TargetTime);

    /// <param name="Start">The Monday.</param>
    /// <param name="End">The Sunday.</param>
    /// <param name="Status">"past", "current" (today is in it) or "future".</param>
    /// <param name="Actual">What was run; none for a future week.</param>
    private sealed record WeekResponse(int WeeksToRace, DateOnly Start, DateOnly End, string Status, ActualResponse? Actual)
    {
        public static WeekResponse From(TrainingBlockWeek week, int? targetPace) => new(
            week.WeeksToRace,
            week.Week.Start,
            week.Week.Start.AddDays(6),
            JsonNamingPolicy.CamelCase.ConvertName(week.Status.ToString()),
            week.Actual is { } actual
                ? new ActualResponse(actual.Runs, actual.Distance, actual.MovingTime, actual.RelativeEffort, LongRunResponse.From(actual.LongRun, targetPace))
                : null);
    }

    /// <param name="Distance">Metres.</param>
    /// <param name="MovingTime">Seconds.</param>
    /// <param name="RelativeEffort">Sum over the week's Runs; none if no Run has one.</param>
    private sealed record ActualResponse(int Runs, double Distance, int MovingTime, double? RelativeEffort, LongRunResponse? LongRun);

    /// <param name="Distance">Metres.</param>
    /// <param name="Pace">Seconds per km.</param>
    /// <param name="PaceVsTarget">
    /// Whole seconds per km slower (positive) or faster (negative) than the Target Pace; none without a Target Time.
    /// </param>
    private sealed record LongRunResponse(long RunId, string Name, DateTime StartLocal, double Distance, double? Pace, int? PaceVsTarget)
    {
        public static LongRunResponse? From(RunSummary? run, int? targetPace)
        {
            if (run is null)
                return null;
            var pace = Run.PaceOf(run.MovingTime, run.Distance);
            return new LongRunResponse(run.Id, run.Name, run.StartLocal, run.Distance, pace, TrainingCalendar.PaceVsTarget(pace, targetPace));
        }
    }
}
