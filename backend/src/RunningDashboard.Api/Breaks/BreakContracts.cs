using System.Globalization;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Breaks;

/// <param name="Kind">"injury", "illness", "holiday" or "other".</param>
/// <param name="Start">The first day.</param>
/// <param name="End">The last day, which may be the first.</param>
/// <param name="Note">Optional; blank is none.</param>
internal sealed record BreakRequest(string? Kind, DateOnly? Start, DateOnly? End, string? Note)
{
    /// <summary>Longer than any holiday; a longer injury is marked as Breaks one after another.</summary>
    public const int MaxDays = 366;

    private static readonly DateOnly EarliestDate = new(1900, 1, 1);

    private static readonly DateOnly LatestDate = new(2099, 12, 31);

    /// <summary>Errors keyed by field, as the form names them; empty if every field is valid.</summary>
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (ApiNames.Parse<BreakKind>(Kind) is null)
            errors["kind"] = ["Pick Injury, Illness, Holiday or Other."];
        if (Start is null)
            errors["start"] = ["Give the first day of the Break."];
        else if (Start < EarliestDate || Start > LatestDate)
            errors["start"] = [$"The first day is between {EarliestDate.Year} and {LatestDate.Year}."];
        if (End is null)
            errors["end"] = ["Give the last day of the Break."];
        else if (End < EarliestDate || End > LatestDate)
            errors["end"] = [$"The last day is between {EarliestDate.Year} and {LatestDate.Year}."];
        else if (Start is { } start && End < start)
            errors["end"] = ["The last day is the first day or after it."];
        else if (Start is { } first && End.Value.DayNumber - first.DayNumber + 1 > MaxDays)
            errors["end"] = [$"A Break is at most {MaxDays} days long; mark a longer one as two Breaks."];
        if (Note?.Trim().Length > Break.MaxNoteLength)
            errors["note"] = [$"The note is at most {Break.MaxNoteLength} characters."];
        return errors;
    }

    /// <summary>Why the Break cannot be next to <paramref name="others"/>, if it cannot: Breaks never overlap.</summary>
    public string? OverlapError(IEnumerable<Break> others) =>
        others.FirstOrDefault(other => other.Start <= End && Start <= other.End) is { } overlapping
            ? $"It overlaps the {ApiNames.Of(overlapping.Kind)} Break from {Day(overlapping.Start)} to {Day(overlapping.End)}."
            : null;

    /// <summary>Fills in <paramref name="period"/> from a request that passed <see cref="Validate"/>.</summary>
    public void ApplyTo(Break period)
    {
        period.Kind = ApiNames.Parse<BreakKind>(Kind)!.Value;
        period.Start = Start!.Value;
        period.End = End!.Value;
        period.Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
    }

    private static string Day(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
}

/// <param name="Kind">"injury", "illness", "holiday" or "other".</param>
/// <param name="Start">The first day.</param>
/// <param name="End">The last day, which may be the first.</param>
/// <param name="Note">None without one.</param>
internal sealed record BreakResponse(int Id, string Kind, DateOnly Start, DateOnly End, string? Note)
{
    public static BreakResponse From(Break period) => new(period.Id, ApiNames.Of(period.Kind), period.Start, period.End, period.Note);
}
