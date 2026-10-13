namespace RunningDashboard.Api.Persistence;

public enum RunDetailStatus
{
    /// <summary>The import has not fetched the detailed Activity yet.</summary>
    Pending,

    /// <summary>Best Efforts and splits are stored.</summary>
    Loaded,

    /// <summary>Strava no longer had the detailed Activity (it answered 404).</summary>
    Unavailable,
}
