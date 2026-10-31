namespace RunningDashboard.Api.Strava;

/// <summary>Links to the Athlete's pages on strava.com.</summary>
public static class StravaLinks
{
    /// <summary>The Activity's page on Strava, by Strava's activity id.</summary>
    public static string Activity(long id) => $"https://www.strava.com/activities/{id}";
}
