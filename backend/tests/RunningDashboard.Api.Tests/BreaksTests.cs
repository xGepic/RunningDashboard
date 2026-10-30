using System.Net;
using System.Net.Http.Json;
using RunningDashboard.Api.Tests.Harness;

namespace RunningDashboard.Api.Tests;

public sealed class BreaksTests
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
    public async Task There_are_no_breaks_until_one_is_marked()
    {
        (await GetBreaksAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task A_marked_break_is_listed_with_its_kind_days_and_note()
    {
        var response = await _browser.PostAsJsonAsync("/api/breaks", new BreakRequest("injury", new(2026, 3, 3), new(2026, 3, 24), "  Calf strain "));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<BreakResponse>())!;
        created.ShouldBe(new BreakResponse(created.Id, "injury", new(2026, 3, 3), new(2026, 3, 24), "Calf strain"));
        response.Headers.Location!.ToString().ShouldBe($"/api/breaks/{created.Id}");
        (await GetBreaksAsync()).ShouldBe([created]);
    }

    [Test]
    public async Task A_break_of_one_day_without_a_note_is_fine()
    {
        await _browser.PostAsJsonAsync("/api/breaks", new BreakRequest("Illness", new(2026, 5, 1), new(2026, 5, 1), "   "));

        var listed = (await GetBreaksAsync()).ShouldHaveSingleItem();
        listed.Kind.ShouldBe("illness");
        listed.Note.ShouldBeNull();
    }

    [Test]
    public async Task Breaks_are_listed_oldest_first()
    {
        await _browser.PostAsJsonAsync("/api/breaks", new BreakRequest("holiday", new(2026, 8, 1), new(2026, 8, 14), null));
        await _browser.PostAsJsonAsync("/api/breaks", new BreakRequest("injury", new(2025, 11, 2), new(2025, 11, 20), null));

        (await GetBreaksAsync()).Select(b => b.Start).ShouldBe([new DateOnly(2025, 11, 2), new DateOnly(2026, 8, 1)]);
    }

    [Test]
    public async Task A_break_can_be_changed()
    {
        var created = await AddAsync(new BreakRequest("holiday", new(2026, 8, 1), new(2026, 8, 14), null));

        var response = await _browser.PutAsJsonAsync($"/api/breaks/{created.Id}", new BreakRequest("other", new(2026, 8, 3), new(2026, 8, 16), "Moving house"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetBreaksAsync()).ShouldBe([new BreakResponse(created.Id, "other", new(2026, 8, 3), new(2026, 8, 16), "Moving house")]);
    }

    [Test]
    public async Task A_removed_break_is_gone()
    {
        var created = await AddAsync(new BreakRequest("holiday", new(2026, 8, 1), new(2026, 8, 14), null));

        var response = await _browser.DeleteAsync($"/api/breaks/{created.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetBreaksAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task A_break_that_does_not_exist_cannot_be_changed_or_removed()
    {
        (await _browser.PutAsJsonAsync("/api/breaks/99", new BreakRequest("holiday", new(2026, 8, 1), new(2026, 8, 14), null)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _browser.DeleteAsync("/api/breaks/99")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_break_needs_a_known_kind_and_both_days()
    {
        var problem = await RejectedAsync(new BreakRequest("vacation", null, null, null));

        problem.Errors["kind"].ShouldBe(["Pick Injury, Illness, Holiday or Other."]);
        problem.Errors["start"].ShouldBe(["Give the first day of the Break."]);
        problem.Errors["end"].ShouldBe(["Give the last day of the Break."]);
    }

    [Test]
    public async Task A_break_cannot_end_before_it_starts()
    {
        var problem = await RejectedAsync(new BreakRequest("injury", new(2026, 3, 24), new(2026, 3, 3), null));

        problem.Errors["end"].ShouldBe(["The last day is the first day or after it."]);
    }

    [Test]
    public async Task A_break_is_at_most_a_year_long()
    {
        (await AddAsync(new BreakRequest("injury", new(2025, 1, 1), new(2026, 1, 1), null))).ShouldNotBeNull();

        var problem = await RejectedAsync(new BreakRequest("injury", new(2026, 3, 1), new(2027, 3, 2), null));

        problem.Errors["end"].ShouldBe(["A Break is at most 366 days long; mark a longer one as two Breaks."]);
    }

    [Test]
    public async Task A_note_is_at_most_200_characters()
    {
        var problem = await RejectedAsync(new BreakRequest("other", new(2026, 3, 1), new(2026, 3, 2), new string('x', 201)));

        problem.Errors["note"].ShouldBe(["The note is at most 200 characters."]);
    }

    [Test]
    public async Task Breaks_do_not_overlap()
    {
        await AddAsync(new BreakRequest("injury", new(2026, 3, 3), new(2026, 3, 24), null));

        var problem = await RejectedAsync(new BreakRequest("holiday", new(2026, 3, 24), new(2026, 3, 30), null));

        problem.Errors["start"].ShouldBe(["It overlaps the injury Break from 3 Mar 2026 to 24 Mar 2026."]);
        (await AddAsync(new BreakRequest("holiday", new(2026, 3, 25), new(2026, 3, 30), null))).ShouldNotBeNull();
    }

    [Test]
    public async Task A_break_changed_within_its_own_days_does_not_overlap_itself()
    {
        var created = await AddAsync(new BreakRequest("injury", new(2026, 3, 3), new(2026, 3, 24), null));

        var response = await _browser.PutAsJsonAsync($"/api/breaks/{created.Id}", new BreakRequest("injury", new(2026, 3, 5), new(2026, 3, 20), null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<BreakResponse> AddAsync(BreakRequest request)
    {
        var response = await _browser.PostAsJsonAsync("/api/breaks", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<BreakResponse>())!;
    }

    private async Task<ValidationProblem> RejectedAsync(BreakRequest request)
    {
        var response = await _browser.PostAsJsonAsync("/api/breaks", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        return (await response.Content.ReadFromJsonAsync<ValidationProblem>())!;
    }

    private async Task<List<BreakResponse>> GetBreaksAsync() =>
        (await _browser.GetFromJsonAsync<List<BreakResponse>>("/api/breaks"))!;

    private sealed record BreakRequest(string? Kind, DateOnly? Start, DateOnly? End, string? Note);

    private sealed record BreakResponse(int Id, string Kind, DateOnly Start, DateOnly End, string? Note);

    private sealed record ValidationProblem(string Title, Dictionary<string, string[]> Errors);
}
