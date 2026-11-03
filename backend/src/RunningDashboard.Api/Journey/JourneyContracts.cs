namespace RunningDashboard.Api.Journey;

/// <summary>One calendar year on the Virtual Journey.</summary>
/// <param name="Distance">The year's running distance, in metres; past <paramref name="Length"/> once the tour is complete.</param>
/// <param name="Length">The year's tour, in metres as the crow flies.</param>
/// <param name="YearlyGoal">The year's Yearly Goal in metres, which the tour was picked for; none without one.</param>
/// <param name="Stops">The start, then each Stop in order; the last is the start again.</param>
internal sealed record JourneyResponse(int Year, double Distance, double Length, double? YearlyGoal, IReadOnlyList<JourneyStopResponse> Stops)
{
    /// <param name="yearlyGoal">Metres; the tour is the longest that fits within it.</param>
    public static JourneyResponse Of(int year, IEnumerable<JourneyRun> runs, double? yearlyGoal)
    {
        var tour = VirtualJourney.TourFor(yearlyGoal);
        var cities = tour.Cities;
        var at = tour.At;
        var reached = new JourneyArrivalResponse?[cities.Count];
        var distance = 0.0;
        // Ties by id, so the same Runs always reach the same Stops.
        foreach (var run in runs.OrderBy(r => r.StartLocal).ThenBy(r => r.Id))
        {
            distance += run.Distance;
            // The start is never reached: the Athlete sets off from it.
            for (var stop = 1; stop < cities.Count && at[stop] <= distance; stop++)
                reached[stop] ??= new JourneyArrivalResponse(DateOnly.FromDateTime(run.StartLocal), run.Id, run.Name);
        }

        var stops = cities
            .Select((city, i) => new JourneyStopResponse(city.Name, city.Country, city.Latitude, city.Longitude, at[i], reached[i]))
            .ToList();
        return new JourneyResponse(year, distance, tour.Length, yearlyGoal, stops);
    }
}

/// <param name="At">Metres from the start along the tour.</param>
/// <param name="Reached">None for the start and for a Stop not reached yet.</param>
internal sealed record JourneyStopResponse(string Name, string Country, double Latitude, double Longitude, double At, JourneyArrivalResponse? Reached);

/// <summary>The Run that brought the year's distance up to a Stop, and its local date.</summary>
internal sealed record JourneyArrivalResponse(DateOnly Date, long RunId, string RunName);

/// <param name="Years">The years with Runs and the current year, newest first.</param>
internal sealed record JourneyPageResponse(JourneyResponse Journey, IReadOnlyList<int> Years);
