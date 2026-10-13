using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

/// <summary>The Athlete's settings: for now only the Distance Unit, km or mi.</summary>
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
    public async Task Distances_are_in_kilometres_until_the_athlete_chooses_otherwise()
    {
        (await GetSettingsAsync()).ShouldBe(new SettingsResponse("km"));
    }

    [Test]
    public async Task Miles_once_chosen_stay_chosen_after_a_restart()
    {
        var response = await _browser.PutAsJsonAsync("/api/settings", new SettingsRequest("mi"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<SettingsResponse>()).ShouldBe(new SettingsResponse("mi"));
        await using var restarted = new TestApp(_app.DatabasePath);
        (await restarted.CreateBrowser().GetFromJsonAsync<SettingsResponse>("/api/settings")).ShouldBe(new SettingsResponse("mi"));
    }

    [Test]
    public async Task Choosing_again_replaces_the_unit()
    {
        await _browser.PutAsJsonAsync("/api/settings", new SettingsRequest("mi"));
        await _browser.PutAsJsonAsync("/api/settings", new SettingsRequest("km"));

        (await GetSettingsAsync()).ShouldBe(new SettingsResponse("km"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("miles")]
    [TestCase("KM")]
    public async Task A_unit_other_than_km_or_mi_is_rejected(string? unit)
    {
        await _browser.PutAsJsonAsync("/api/settings", new SettingsRequest("mi"));

        var response = await _browser.PutAsJsonAsync("/api/settings", new SettingsRequest(unit));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        problem!.Errors["distanceUnit"].ShouldBe(["The Distance Unit is km or mi."]);
        (await GetSettingsAsync()).ShouldBe(new SettingsResponse("mi"));
    }

    private async Task<SettingsResponse> GetSettingsAsync() =>
        (await _browser.GetFromJsonAsync<SettingsResponse>("/api/settings"))!;

    private sealed record SettingsRequest(string? DistanceUnit);

    private sealed record SettingsResponse(string DistanceUnit);

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
