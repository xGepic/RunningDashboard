using System.Net.Http.Json;
using RunningDashboard.Api.Fitness;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// Fitness &amp; Freshness, today being Fri 2026-10-09. Each day moves Fitness a 42nd and Fatigue a 7th of the way
/// towards the day's Training Load, from zero on the first day with a Run:
///
/// | Day        | Load           | Fitness                        | Fatigue                      | Form   |
/// |------------|----------------|--------------------------------|------------------------------|--------|
/// | Wed 7 Oct  | 42             | 42 / 42 = 1                    | 42 / 7 = 6                   | −5     |
/// | Thu 8 Oct  | 21 + 7 = 28    | 1 + 27 / 42 = 1.643            | 6 + 22 / 7 = 9.143           | −7.5   |
/// | Fri 9 Oct  | 0              | 1.643 × 41/42 = 1.604          | 9.143 × 6/7 = 7.837          | −6.233 |
/// </summary>
public sealed class FreshnessTests
{
    private readonly TestApp _app = new();

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Each_day_carries_fitness_and_fatigue_towards_the_runs_relative_effort_up_to_today()
    {
        var browser = await _app.ConnectWithActivitiesAsync(
            new TestActivity(1, "Tempo", new DateTime(2026, 10, 7, 18, 0, 0), 10000, 2700, RelativeEffort: 42),
            new TestActivity(2, "Morning", new DateTime(2026, 10, 8, 7, 0, 0), 6000, 2100, RelativeEffort: 21),
            // A treadmill Run loads the body as much as any other.
            new TestActivity(3, "Evening treadmill", new DateTime(2026, 10, 8, 19, 0, 0), 3000, 1000, Trainer: true, RelativeEffort: 7),
            // Rides count toward nothing.
            new TestActivity(4, "Ride", new DateTime(2026, 10, 8, 12, 0, 0), 40000, 5400, "Ride", RelativeEffort: 80),
            // Recorded without heart rate: no Relative Effort, so no load.
            new TestActivity(5, "No strap", new DateTime(2026, 10, 9, 7, 0, 0), 5000, 1800));
        await browser.CreateGoalRaceAsync(new GoalRaceRequest("Spring Half", new DateOnly(2027, 4, 11), 21097, null, 12));

        var freshness = (await browser.GetFromJsonAsync<Freshness>("/api/fitness/freshness"))!;

        freshness.Days.ShouldBe([
            new Day(new DateOnly(2026, 10, 7), 42, 1, 6, -5, 1, "Tempo"),
            // The day's hardest Run names it, to open from the chart.
            new Day(new DateOnly(2026, 10, 8), 28, 1.6, 9.1, -7.5, 2, "Morning"),
            new Day(new DateOnly(2026, 10, 9), 0, 1.6, 7.8, -6.2, 5, "No strap"),
        ]);
        freshness.Races.ShouldBe([new Race(new DateOnly(2027, 4, 11), "Spring Half")]);
    }

    [Test]
    public async Task Is_empty_without_runs()
    {
        var browser = await _app.ConnectWithActivitiesAsync();

        var freshness = (await browser.GetFromJsonAsync<Freshness>("/api/fitness/freshness"))!;

        freshness.Days.ShouldBeEmpty();
    }

    [Test]
    public void Fitness_and_fatigue_fade_on_days_without_runs_and_settle_on_a_steady_load()
    {
        var start = new DateOnly(2026, 1, 1);
        var everyDay = Enumerable.Range(0, 400).ToDictionary(i => start.AddDays(i), _ => 50.0);

        var days = FitnessFreshness.Of(everyDay, start.AddDays(399 + 7));

        // After a year of the same load every day, both have caught up with it and Form is zero.
        var steady = days[399];
        steady.Fitness.ShouldBe(50, tolerance: 0.01);
        steady.Fatigue.ShouldBe(50, tolerance: 0.01);
        steady.Form.ShouldBe(0, tolerance: 0.01);
        // A week off: Fatigue falls much faster than Fitness, so Form turns positive.
        var weekOff = days[^1];
        weekOff.Fatigue.ShouldBe(50 * Math.Pow(6.0 / 7, 7), tolerance: 0.01);
        weekOff.Fitness.ShouldBe(50 * Math.Pow(41.0 / 42, 7), tolerance: 0.01);
        weekOff.Form.ShouldBeGreaterThan(20);
    }

    private sealed record Freshness(Day[] Days, Race[] Races);

    private sealed record Day(DateOnly Date, double Load, double Fitness, double Fatigue, double Form, long? RunId, string? RunName);

    private sealed record Race(DateOnly Date, string Name);
}
