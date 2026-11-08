using System.Net.Http.Headers;

namespace RunningDashboard.Api.Strava;

/// <summary>
/// Tracks Strava's rate limits from the headers of its responses and holds requests back until the window resets,
/// so no request is sent that Strava would refuse. Strava counts per 15 minutes (windows start at :00, :15, :30 and
/// :45) and per UTC day, separately for all requests and for reads.
/// </summary>
public sealed class StravaRateLimits(TimeProvider clock)
{
    private static readonly TimeSpan FifteenMinutes = TimeSpan.FromMinutes(15);

    private readonly Lock _lock = new();
    private readonly Dictionary<Window, Usage> _usage = [];
    private DateTimeOffset? _refusedUntil;
    private DateTimeOffset? _pausedUntil;

    /// <summary>Set while a request is held back; the sync status shows it.</summary>
    public DateTimeOffset? PausedUntil
    {
        get
        {
            lock (_lock)
                return _pausedUntil;
        }
    }

    /// <summary>Waits until Strava will accept another request; returns at once when there is capacity.</summary>
    public async Task WaitForCapacityAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var now = clock.GetUtcNow();
            if (ResumeAt(now) is not { } resumeAt || resumeAt <= now)
                return;

            // Start the timer before announcing the pause, so whoever reacts to the pause cannot outrun it.
            var wait = Task.Delay(resumeAt - now, clock, cancellationToken);
            lock (_lock)
                _pausedUntil = resumeAt;
            try
            {
                await wait;
            }
            finally
            {
                lock (_lock)
                    _pausedUntil = null;
            }
        }
    }

    public void Observe(HttpResponseHeaders headers)
    {
        var now = clock.GetUtcNow();
        lock (_lock)
        {
            Read(headers, "X-RateLimit-Limit", "X-RateLimit-Usage", RequestKind.All, now);
            Read(headers, "X-ReadRateLimit-Limit", "X-ReadRateLimit-Usage", RequestKind.Read, now);
        }
    }

    /// <summary>
    /// Strava answered 429: whatever the headers say, send nothing before the next 15-minute window.
    /// </summary>
    public void ObserveRefusal(HttpResponseHeaders headers)
    {
        Observe(headers);
        lock (_lock)
            _refusedUntil = NextQuarterHour(clock.GetUtcNow());
    }

    /// <summary>
    /// Strava's four counters as they stand now, reads first, each per 15 minutes and then per day; none until Strava has
    /// answered since the backend started. A counter is back at zero once its period has turned over since Strava
    /// reported it.
    /// </summary>
    public IReadOnlyList<RateLimitUsage> CurrentUsage()
    {
        var now = clock.GetUtcNow();
        lock (_lock)
        {
            return [.. _usage
                .OrderBy(u => u.Key.Kind != RequestKind.Read)
                .ThenBy(u => u.Key.Daily)
                .Select(u =>
                {
                    var (periodStart, nextPeriod) = PeriodOf(u.Key, now);
                    var used = u.Value.ObservedAt >= periodStart ? u.Value.Used : 0;
                    return new RateLimitUsage(u.Key.Kind == RequestKind.Read, u.Key.Daily, used, u.Value.Limit, nextPeriod, u.Value.ObservedAt);
                })];
        }
    }

    private DateTimeOffset? ResumeAt(DateTimeOffset now)
    {
        lock (_lock)
        {
            DateTimeOffset? resumeAt = _refusedUntil > now ? _refusedUntil : null;
            foreach (var (window, usage) in _usage)
            {
                var (periodStart, nextPeriod) = PeriodOf(window, now);
                if (usage.Used >= usage.Limit && usage.ObservedAt >= periodStart && (resumeAt is null || nextPeriod > resumeAt))
                    resumeAt = nextPeriod;
            }

            return resumeAt;
        }
    }

    /// <summary>The 15 minutes or the UTC day that <paramref name="now"/> falls in, for <paramref name="window"/>.</summary>
    private static (DateTimeOffset Start, DateTimeOffset Next) PeriodOf(Window window, DateTimeOffset now)
    {
        if (window.Daily)
        {
            var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
            return (dayStart, dayStart.AddDays(1));
        }

        var nextQuarter = NextQuarterHour(now);
        return (nextQuarter - FifteenMinutes, nextQuarter);
    }

    /// <summary>Headers look like "X-ReadRateLimit-Limit: 100,1000" and "X-ReadRateLimit-Usage: 12,350".</summary>
    private void Read(HttpResponseHeaders headers, string limitHeader, string usageHeader, RequestKind kind, DateTimeOffset now)
    {
        if (!headers.TryGetValues(limitHeader, out var limitValues) || !headers.TryGetValues(usageHeader, out var usageValues))
            return;

        var limits = limitValues.First().Split(',');
        var used = usageValues.First().Split(',');
        if (limits.Length != 2 || used.Length != 2)
            return;

        _usage[new Window(kind, Daily: false)] = new Usage(int.Parse(limits[0]), int.Parse(used[0]), now);
        _usage[new Window(kind, Daily: true)] = new Usage(int.Parse(limits[1]), int.Parse(used[1]), now);
    }

    private static DateTimeOffset NextQuarterHour(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        var quarterStart = new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute / 15 * 15, 0, TimeSpan.Zero);
        return quarterStart + FifteenMinutes;
    }

    private enum RequestKind
    {
        All,
        Read,
    }

    /// <summary>One of Strava's four counters: all or read requests, per 15 minutes or per UTC day.</summary>
    private readonly record struct Window(RequestKind Kind, bool Daily);

    private sealed record Usage(int Limit, int Used, DateTimeOffset ObservedAt);
}
