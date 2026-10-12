namespace RunningDashboard.Api.Runs;

/// <summary>
/// What the Runs list asks for, from the query string: search, date range, sort order and page.
/// </summary>
/// <param name="Search">Part of the Run's name, ignoring case.</param>
/// <param name="From">First local start date to include.</param>
/// <param name="To">Last local start date to include.</param>
/// <param name="Sort">date (default), distance, movingTime or pace.</param>
/// <param name="Direction">desc (default) or asc.</param>
/// <param name="Page">1-based (default 1).</param>
/// <param name="PageSize">1 to 200 (default 50).</param>
public sealed record RunListQuery(
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

    private static readonly string[] SortKeys = ["date", "distance", "movingTime", "pace"];
    private static readonly string[] Directions = ["asc", "desc"];

    public int PageOrDefault => Page ?? 1;

    public int PageSizeOrDefault => PageSize ?? DefaultPageSize;

    public bool Ascending => Direction == "asc";

    public string SortKey => Sort ?? "date";

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (PageOrDefault < 1)
            errors[nameof(Page)] = ["Pages start at 1."];
        if (PageSizeOrDefault is < 1 or > MaxPageSize)
            errors[nameof(PageSize)] = [$"A page holds 1 to {MaxPageSize} Runs."];
        if (Sort is not null && !SortKeys.Contains(Sort))
            errors[nameof(Sort)] = [$"Sort by one of: {string.Join(", ", SortKeys)}."];
        if (Direction is not null && !Directions.Contains(Direction))
            errors[nameof(Direction)] = ["Direction is asc or desc."];
        if (From > To)
            errors[nameof(From)] = ["The range starts after it ends."];
        return errors;
    }
}
