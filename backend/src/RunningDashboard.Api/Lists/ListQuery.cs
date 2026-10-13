namespace RunningDashboard.Api.Lists;

/// <summary>
/// What a history list (Runs, Rides or Swims) asks for, from the query string: search, date range, sort order and page.
/// Each list names the figures it sorts by; date is always one of them.
/// </summary>
/// <param name="Search">Part of the name, ignoring case.</param>
/// <param name="From">First local start date to include.</param>
/// <param name="To">Last local start date to include.</param>
/// <param name="Sort">date (default) or one of the list's figures.</param>
/// <param name="Direction">desc (default) or asc.</param>
/// <param name="Page">1-based (default 1).</param>
/// <param name="PageSize">1 to 200 (default 50).</param>
public sealed record ListQuery(
    string? Search,
    DateOnly? From,
    DateOnly? To,
    string? Sort,
    string? Direction,
    int? Page,
    int? PageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    private const string DateKey = "date";
    private static readonly string[] Directions = ["asc", "desc"];

    public int PageOrDefault => Page ?? 1;

    public int PageSizeOrDefault => PageSize ?? DefaultPageSize;

    /// <param name="figures">The list's sort keys besides date, e.g. distance.</param>
    /// <param name="items">What the list holds, e.g. "Runs", for the messages.</param>
    public Dictionary<string, string[]> Validate(IEnumerable<string> figures, string items)
    {
        var sortKeys = new[] { DateKey }.Concat(figures).ToList();
        var errors = new Dictionary<string, string[]>();
        if (PageOrDefault < 1)
            errors[nameof(Page)] = ["Pages start at 1."];
        if (PageSizeOrDefault is < 1 or > MaxPageSize)
            errors[nameof(PageSize)] = [$"A page holds 1 to {MaxPageSize} {items}."];
        if (Sort is not null && !sortKeys.Contains(Sort))
            errors[nameof(Sort)] = [$"Sort by one of: {string.Join(", ", sortKeys)}."];
        if (Direction is not null && !Directions.Contains(Direction))
            errors[nameof(Direction)] = ["Direction is asc or desc."];
        if (From > To)
            errors[nameof(From)] = ["The range starts after it ends."];
        return errors;
    }

    /// <summary>Whether an item named <paramref name="name"/> that started at <paramref name="startLocal"/> is asked for.</summary>
    public bool Matches(string name, DateTime startLocal) =>
        (string.IsNullOrWhiteSpace(Search) || name.Contains(Search.Trim(), StringComparison.InvariantCultureIgnoreCase))
        && (From is null || DateOnly.FromDateTime(startLocal) >= From)
        && (To is null || DateOnly.FromDateTime(startLocal) <= To);

    /// <summary>
    /// The page asked for, in the requested order. Items without the figure sorted by go last; equal ones fall back to
    /// newest first.
    /// </summary>
    /// <param name="figures">Each sort key besides date, with the figure it sorts by.</param>
    public List<T> PageOf<T>(IEnumerable<T> items, Func<T, DateTime> startLocal, IReadOnlyDictionary<string, Func<T, double?>> figures)
    {
        var ascending = Direction == "asc";
        var sorted = Sort is null or DateKey
            ? ascending ? items.OrderBy(startLocal) : items.OrderByDescending(startLocal)
            : items.OrderBy(i => figures[Sort](i) is null).ThenBy(i => ascending ? figures[Sort](i) : -figures[Sort](i));
        return [.. sorted
            .ThenByDescending(startLocal)
            .Skip((PageOrDefault - 1) * PageSizeOrDefault)
            .Take(PageSizeOrDefault)];
    }
}
