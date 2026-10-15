namespace RunningDashboard.Api.Sync;

/// <summary>How often the backend syncs by itself while it runs: a setting for the PC, so it lives in appsettings and not in the UI.</summary>
public sealed class SyncOptions
{
    public const string Section = "Sync";

    /// <summary>
    /// Time between the syncs the backend starts by itself, every hour unless set. A sync after the first import takes
    /// a few requests, so 24 a day stay far below Strava's daily limit. Zero turns them off, leaving the syncs on start,
    /// after connecting and on "Sync now".
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
}
