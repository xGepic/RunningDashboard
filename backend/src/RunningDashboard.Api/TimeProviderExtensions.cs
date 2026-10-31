namespace RunningDashboard.Api;

public static class TimeProviderExtensions
{
    /// <summary>Today's date where the backend runs, which is where the Athlete is.</summary>
    public static DateOnly Today(this TimeProvider clock) => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}
