using System.Net;
using System.Text;

namespace RunningDashboard.Api.Tests.Harness;

/// <summary>
/// Stands in for strava.com at the HTTP level. Responses are Strava JSON from the Fixtures/Strava folder,
/// queued per path and served in order; every request the app sends is kept for assertions. The sync's recheck of the
/// most recent Activities has its own queue (<see cref="RecentActivitiesPath"/>) and finds none unless a test queues
/// some; the athlete (for the Shoes) has no Shoes unless a test queues one. Anything else without a queued response
/// is 404 (as for an Activity Strava does not have).
/// </summary>
public sealed class FakeStrava : HttpMessageHandler
{
    private const string ActivityListPath = "/api/v3/athlete/activities";

    private const string AthletePath = "/api/v3/athlete";

    /// <summary>Queue responses here for the sync's one request for the 20 most recent Activities.</summary>
    public const string RecentActivitiesPath = ActivityListPath + "#recent";

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

    /// <summary>Serves <paramref name="activities"/> as one page of Strava's Activity list.</summary>
    public void RespondWithActivities(string path, params TestActivity[] activities) =>
        Respond(HttpMethod.Get, path, () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(TestActivity.ToJson(activities), Encoding.UTF8, "application/json"),
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

    public static HttpResponseMessage FixtureResponse(string fixture) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Strava", fixture)),
            Encoding.UTF8,
            "application/json"),
    };

    private static string QueuePath(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        return path == ActivityListPath && request.RequestUri.Query.Contains("per_page=20&") ? RecentActivitiesPath : path;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Func<Task<HttpResponseMessage>>? respond = null;
        lock (_responses)
        {
            Requests.Add(new RecordedRequest(request.RequestUri!, request.Headers.Authorization?.ToString(), body));
            if (_responses.TryGetValue((request.Method, QueuePath(request)), out var queue) && queue.Count > 0)
                respond = queue.Dequeue();
        }

        if (respond is not null)
            return await respond().WaitAsync(cancellationToken);

        if (request.Method == HttpMethod.Get && QueuePath(request) == RecentActivitiesPath)
            return FixtureResponse("activities-empty.json");
        if (request.Method == HttpMethod.Get && QueuePath(request) == AthletePath)
            return FixtureResponse("athlete-without-shoes.json");

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"FakeStrava has no response for {request.Method} {request.RequestUri}"),
        };
    }
}

public sealed record RecordedRequest(Uri Uri, string? Authorization, string? Body);
