namespace RunningDashboard.Api.Tests.Harness;

/// <summary>
/// The Goal Races most tests use, against the test clock's Fri 9 Oct 2026: the first marathon is just past, the second
/// is the upcoming one. A test that needs a different Training Block takes one with <c>with</c>.
/// </summary>
public static class TestRaces
{
    /// <summary>Metres.</summary>
    public const double Marathon = 42195;

    /// <summary>Sun 27 Sep 2026, no Target Time, 14 weeks: Mon 22 Jun to Sun 27 Sep 2026.</summary>
    public static readonly GoalRaceRequest FirstMarathon = new("Vienna Marathon", new DateOnly(2026, 9, 27), Marathon, null, 14);

    /// <summary>Sun 23 May 2027, 5 hours Target Time, 16 weeks: Mon 1 Feb to Sun 23 May 2027.</summary>
    public static readonly GoalRaceRequest SecondMarathon = new("Second marathon", new DateOnly(2027, 5, 23), Marathon, 5 * 3600, 16);
}
