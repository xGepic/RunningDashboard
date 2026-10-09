using System.Net;
using System.Text;

namespace RunningDashboard.Api.Tests.Harness;

/// <summary>
/// Stands in for strava.com at the HTTP level. Responses are recorded Strava JSON
/// from the Fixtures/Strava folder; every request the app sends is kept for assertions.
/// </summary>
public sealed class FakeStrava : HttpMessageHandler
{
    private readonly Dictionary<(HttpMethod, string), Queue<Func<HttpResponseMessage>>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public void RespondWithFixture(HttpMethod method, string path, string fixture) =>
        Respond(method, path, () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ReadFixture(fixture), Encoding.UTF8, "application/json"),
        });

    public void Respond(HttpMethod method, string path, Func<HttpResponseMessage> response)
    {
        if (!_responses.TryGetValue((method, path), out var queue))
            _responses[(method, path)] = queue = new Queue<Func<HttpResponseMessage>>();
        queue.Enqueue(response);
    }

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Strava", name));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.RequestUri!, body));

        var key = (request.Method, request.RequestUri!.AbsolutePath);
        if (_responses.TryGetValue(key, out var queue) && queue.Count > 0)
            return queue.Dequeue()();

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"FakeStrava has no response for {request.Method} {request.RequestUri}"),
        };
    }
}

public sealed record RecordedRequest(Uri Uri, string? Body);
