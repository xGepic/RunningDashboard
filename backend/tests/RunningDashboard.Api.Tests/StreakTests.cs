using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Streaks on the Overview: consecutive Training Weeks with at least one Run. The clock stands at Friday 9 Oct 2026,
/// in the week of Mon 5 Oct, unless a test moves it.
/// </summary>
public sealed class StreakTests
{
    private readonly TestApp _app = new();

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task On_monday_before_the_first_run_of_the_week_the_streak_counts_up_to_last_week()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 9, 21)),
            Run(2, new DateOnly(2026, 9, 28)),
            Run(3, new DateOnly(2026, 10, 5)));
        // Monday 12 Oct, before any Run of the week.
        _app.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));

        var streak = (await GetOverviewAsync(browser)).Streak;

        streak.Current.ShouldBe(3);
    }

    [Test]
    public async Task The_current_week_adds_to_the_streak_once_it_has_a_run()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 9, 21)),
            Run(2, new DateOnly(2026, 9, 28)),
            Run(3, new DateOnly(2026, 10, 5)));

        var streak = (await GetOverviewAsync(browser)).Streak;

        streak.Current.ShouldBe(3);
        streak.Longest.ShouldBe(new LongestStreakResponse(3, new DateOnly(2026, 9, 21), new DateOnly(2026, 10, 5)));
    }

    [Test]
    public async Task A_full_week_without_a_run_resets_the_streak_to_zero()
    {
        // Nothing in the week of Mon 28 Sep, nor so far in the current one.
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2026, 9, 14)),
            Run(2, new DateOnly(2026, 9, 21)));

        var streak = (await GetOverviewAsync(browser)).Streak;

        streak.Current.ShouldBe(0);
        streak.Longest.ShouldBe(new LongestStreakResponse(2, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 21)));
    }

    [Test]
    public async Task The_longest_streak_can_be_from_an_earlier_year()
    {
        // Six weeks in spring 2025, from Mon 3 Mar to the week of Mon 7 Apr; a gap in the week of Mon 14 Apr.
        TestActivity[] spring2025 = [.. Enumerable.Range(0, 6).Select(i => Run(i + 1, new DateOnly(2025, 3, 3).AddDays(7 * i)))];
        var browser = await _app.ConnectWithActivitiesAsync(
            [.. spring2025, Run(7, new DateOnly(2025, 4, 21)), Run(8, new DateOnly(2026, 9, 28)), Run(9, new DateOnly(2026, 10, 5))]);

        var streak = (await GetOverviewAsync(browser)).Streak;

        streak.Current.ShouldBe(2);
        streak.Longest.ShouldBe(new LongestStreakResponse(6, new DateOnly(2025, 3, 3), new DateOnly(2025, 4, 7)));
    }

    [Test]
    public async Task Of_equally_long_streaks_the_most_recent_is_the_longest()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            Run(1, new DateOnly(2025, 3, 3)),
            Run(2, new DateOnly(2025, 3, 10)),
            Run(3, new DateOnly(2025, 3, 17)),
            Run(4, new DateOnly(2026, 9, 7)),
            Run(5, new DateOnly(2026, 9, 14)),
            Run(6, new DateOnly(2026, 9, 21)));

        var streak = (await GetOverviewAsync(browser)).Streak;

        streak.Longest.ShouldBe(new LongestStreakResponse(3, new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 21)));
    }

    [Test]
    public async Task Without_runs_the_streak_is_zero_and_there_is_no_longest()
    {
        var browser = _app.CreateBrowser();

        var streak = (await GetOverviewAsync(browser)).Streak;

        streak.ShouldBe(new StreakResponse(0, null));
    }

    /// <summary>A Run on the Wednesday of the Training Week starting <paramref name="monday"/>.</summary>
    private static TestActivity Run(long id, DateOnly monday) =>
        new(id, $"Run of the week of {monday:d MMM yyyy}", monday.AddDays(2).ToDateTime(new TimeOnly(18, 0)), 8000, 2900);

    private static async Task<OverviewResponse> GetOverviewAsync(HttpClient browser) =>
        (await browser.GetFromJsonAsync<OverviewResponse>("/api/overview"))!;

    private sealed record OverviewResponse(StreakResponse Streak);

    /// <param name="Current">Training Weeks.</param>
    /// <param name="Longest">None without Runs.</param>
    private sealed record StreakResponse(int Current, LongestStreakResponse? Longest);

    /// <param name="Weeks">Training Weeks.</param>
    /// <param name="First">The Monday of its first Training Week.</param>
    /// <param name="Last">The Monday of its last Training Week.</param>
    private sealed record LongestStreakResponse(int Weeks, DateOnly First, DateOnly Last);
}
