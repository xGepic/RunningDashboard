using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>
/// The Athlete's settings: the Distance Unit (km or mi) and the Shoes' Maximum Distance. Each is changed on its own,
/// leaving the other as it is.
/// </summary>
public sealed class SettingsTests
{
    private readonly TestApp _app = new();
    private HttpClient _browser = null!;

    [SetUp]
    public void SetUp()
    {
        _browser = _app.CreateBrowser();
    }

    [TearDown]
    public async Task TearDownAsync() => await _app.DisposeAsync();

    [Test]
    public async Task Distances_are_in_kilometres_and_shoes_last_700_km_until_the_athlete_chooses_otherwise()
    {
        (await GetSettingsAsync()).ShouldBe(new SettingsResponse("km", 700_000));
    }

    [Test]
    public async Task Miles_once_chosen_stay_chosen_after_a_restart()
    {
        var response = await ChangeAsync(new { distanceUnit = "mi" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<SettingsResponse>()).ShouldBe(new SettingsResponse("mi", 700_000));
        await using var restarted = new TestApp(_app.DatabasePath);
        (await restarted.CreateBrowser().GetFromJsonAsync<SettingsResponse>("/api/settings")).ShouldBe(new SettingsResponse("mi", 700_000));
    }

    [Test]
    public async Task Choosing_again_replaces_the_unit()
    {
        await ChangeAsync(new { distanceUnit = "mi" });
        await ChangeAsync(new { distanceUnit = "km" });

        (await GetSettingsAsync()).DistanceUnit.ShouldBe("km");
    }

    [Test]
    public async Task The_maximum_distance_and_the_unit_are_each_changed_without_touching_the_other()
    {
        await ChangeAsync(new { distanceUnit = "mi" });
        var response = await ChangeAsync(new { shoeMaximumDistance = 804_672.0 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetSettingsAsync()).ShouldBe(new SettingsResponse("mi", 804_672));
        await ChangeAsync(new { distanceUnit = "km" });
        (await GetSettingsAsync()).ShouldBe(new SettingsResponse("km", 804_672));
    }

    [TestCase("")]
    [TestCase("miles")]
    [TestCase("KM")]
    public async Task A_unit_other_than_km_or_mi_is_rejected(string unit)
    {
        await ChangeAsync(new { distanceUnit = "mi" });

        var response = await ChangeAsync(new { distanceUnit = unit });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        problem!.Errors["distanceUnit"].ShouldBe(["The Distance Unit is km or mi."]);
        (await GetSettingsAsync()).DistanceUnit.ShouldBe("mi");
    }

    [TestCase(0.0)]
    [TestCase(-5000.0)]
    [TestCase(5_000_001.0)]
    public async Task A_maximum_distance_that_is_not_a_sensible_distance_is_rejected(double distance)
    {
        var response = await ChangeAsync(new { shoeMaximumDistance = distance });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        problem!.Errors["shoeMaximumDistance"].ShouldBe(["The Maximum Distance is a positive number of metres, at most 5000 km."]);
        (await GetSettingsAsync()).ShoeMaximumDistance.ShouldBe(700_000);
    }

    [Test]
    public async Task Nothing_is_changed_when_one_of_two_changes_is_rejected()
    {
        var response = await ChangeAsync(new { distanceUnit = "mi", shoeMaximumDistance = 0.0 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetSettingsAsync()).ShouldBe(new SettingsResponse("km", 700_000));
    }

    private Task<HttpResponseMessage> ChangeAsync(object changes) => _browser.PatchAsJsonAsync("/api/settings", changes);

    private async Task<SettingsResponse> GetSettingsAsync() =>
        (await _browser.GetFromJsonAsync<SettingsResponse>("/api/settings"))!;

    /// <param name="ShoeMaximumDistance">Metres.</param>
    private sealed record SettingsResponse(string DistanceUnit, double ShoeMaximumDistance);

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
