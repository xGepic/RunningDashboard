using System.Net;
using System.Text;

namespace RunningDashboard.Api.Tests.Harness;

/// <summary>
/// Stands in for strava.com at the HTTP level. Responses are Strava JSON from the Fixtures/Strava folder,
/// queued per path and served in order; every request the app sends is kept for assertions.
/// </summary>
public sealed class FakeStrava : HttpMessageHandler
{
    private readonly Dictionary<(HttpMethod, string), Queue<Func<Task<HttpResponseMessage>>>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public void RespondWithFixture(HttpMethod method, string path, string fixture) =>
        Respond(method, path, () => FixtureResponse(fixture));

    /// <summary>
    /// Serves the fixture only once <paramref name="release"/> completes, so a test can observe the app mid-request.
    /// </summary>
    public void RespondWithFixtureAfter(Task release, HttpMethod method, string path, string fixture) =>
        Enqueue(method, path, async () =>
        {
            await release;
            return FixtureResponse(fixture);
        });

    public void Respond(HttpMethod method, string path, Func<HttpResponseMessage> response) =>
        Enqueue(method, path, () => Task.FromResult(response()));

    private void Enqueue(HttpMethod method, string path, Func<Task<HttpResponseMessage>> response)
    {
        lock (_responses)
        {
            if (!_responses.TryGetValue((method, path), out var queue))
                _responses[(method, path)] = queue = new Queue<Func<Task<HttpResponseMessage>>>();
            queue.Enqueue(response);
        }
    }

    private static HttpResponseMessage FixtureResponse(string fixture) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Strava", fixture)),
            Encoding.UTF8,
            "application/json"),
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Func<Task<HttpResponseMessage>>? respond = null;
        lock (_responses)
        {
            Requests.Add(new RecordedRequest(request.RequestUri!, request.Headers.Authorization?.ToString(), body));
            if (_responses.TryGetValue((request.Method, request.RequestUri!.AbsolutePath), out var queue) && queue.Count > 0)
                respond = queue.Dequeue();
        }

        return respond is not null
            ? await respond().WaitAsync(cancellationToken)
            : new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"FakeStrava has no response for {request.Method} {request.RequestUri}"),
            };
    }
}

public sealed record RecordedRequest(Uri Uri, string? Authorization, string? Body);
