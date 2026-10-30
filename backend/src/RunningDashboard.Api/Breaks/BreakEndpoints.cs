using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Breaks;

/// <summary>
/// Lists, adds, changes and removes the Breaks. They are only ever shown, so no other response changes with them.
/// </summary>
public static class BreakEndpoints
{
    public static void MapBreakEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/breaks", async (AppDbContext db, CancellationToken cancellationToken) =>
            (await db.Breaks.AsNoTracking().OrderBy(b => b.Start).ToListAsync(cancellationToken)).Select(BreakResponse.From));

        app.MapPost("/api/breaks", async (BreakRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (await RejectAsync(request, null, db, cancellationToken) is { } rejected)
                return rejected;

            var period = new Break();
            request.ApplyTo(period);
            db.Breaks.Add(period);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/breaks/{period.Id}", BreakResponse.From(period));
        });

        app.MapPut("/api/breaks/{id:int}", async (int id, BreakRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var period = await db.Breaks.FindAsync([id], cancellationToken);
            if (period is null)
                return Results.NotFound();
            if (await RejectAsync(request, id, db, cancellationToken) is { } rejected)
                return rejected;

            request.ApplyTo(period);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(BreakResponse.From(period));
        });

        app.MapDelete("/api/breaks/{id:int}", async (int id, AppDbContext db, CancellationToken cancellationToken) =>
            await db.Breaks.Where(b => b.Id == id).ExecuteDeleteAsync(cancellationToken) == 0
                ? Results.NotFound()
                : Results.NoContent());
    }

    /// <summary>Problem details for invalid fields or an overlap with another Break; null if it can be saved.</summary>
    private static async Task<IResult?> RejectAsync(BreakRequest request, int? id, AppDbContext db, CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var others = await db.Breaks.AsNoTracking().Where(b => b.Id != id).ToListAsync(cancellationToken);
        return request.OverlapError(others) is { } overlap
            ? Results.ValidationProblem(new Dictionary<string, string[]> { ["start"] = [overlap] })
            : null;
    }
}
