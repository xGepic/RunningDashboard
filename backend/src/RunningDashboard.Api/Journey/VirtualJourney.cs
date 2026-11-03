using Microsoft.EntityFrameworkCore;
using RunningDashboard.Api.Persistence;

namespace RunningDashboard.Api.Journey;

/// <summary>A city on the Virtual Journey, by its centre.</summary>
internal sealed record JourneyCity(string Name, string Country, double Latitude, double Longitude);

/// <summary>A Run as the Virtual Journey needs it.</summary>
/// <param name="Distance">Metres.</param>
internal sealed record JourneyRun(long Id, string Name, DateTime StartLocal, double Distance);

/// <summary>A loop from Vienna through cities and back, each leg the great-circle distance between two cities.</summary>
/// <param name="cities">The start, then the Stops in order; the last is the start again.</param>
internal sealed class JourneyTour(params JourneyCity[] cities)
{
    public IReadOnlyList<JourneyCity> Cities { get; } = cities;

    /// <summary>Each city's distance from the start along the tour, in metres.</summary>
    public IReadOnlyList<double> At { get; } = DistancesAlong(cities);

    /// <summary>Metres.</summary>
    public double Length => At[^1];

    private static double[] DistancesAlong(JourneyCity[] cities)
    {
        var at = new double[cities.Length];
        for (var i = 1; i < cities.Length; i++)
            at[i] = at[i - 1] + VirtualJourney.GreatCircle(cities[i - 1], cities[i]);
        return at;
    }
}

/// <summary>
/// The Virtual Journey: each calendar year, the year's running distance carries the Athlete along a loop of real cities
/// from Vienna and back. The year's tour is the longest that fits within its Yearly Goal, so reaching the goal always
/// completes it; without a goal, the capitals tour.
/// </summary>
internal static class VirtualJourney
{
    private static readonly JourneyCity Vienna = new("Vienna", "Austria", 48.2082, 16.3738);
    private static readonly JourneyCity Amsterdam = new("Amsterdam", "Netherlands", 52.3676, 4.9041);
    private static readonly JourneyCity Berlin = new("Berlin", "Germany", 52.5200, 13.4050);
    private static readonly JourneyCity Bern = new("Bern", "Switzerland", 46.9480, 7.4474);
    private static readonly JourneyCity Bologna = new("Bologna", "Italy", 44.4949, 11.3426);
    private static readonly JourneyCity Bolzano = new("Bolzano", "Italy", 46.4983, 11.3548);
    private static readonly JourneyCity Bratislava = new("Bratislava", "Slovakia", 48.1486, 17.1077);
    private static readonly JourneyCity Brno = new("Brno", "Czechia", 49.1951, 16.6068);
    private static readonly JourneyCity Brussels = new("Brussels", "Belgium", 50.8503, 4.3517);
    private static readonly JourneyCity Budapest = new("Budapest", "Hungary", 47.4979, 19.0402);
    private static readonly JourneyCity CeskeBudejovice = new("České Budějovice", "Czechia", 48.9745, 14.4743);
    private static readonly JourneyCity Dresden = new("Dresden", "Germany", 51.0504, 13.7373);
    private static readonly JourneyCity Frankfurt = new("Frankfurt", "Germany", 50.1109, 8.6821);
    private static readonly JourneyCity Graz = new("Graz", "Austria", 47.0707, 15.4395);
    private static readonly JourneyCity Gyor = new("Győr", "Hungary", 47.6875, 17.6504);
    private static readonly JourneyCity Hamburg = new("Hamburg", "Germany", 53.5511, 9.9937);
    private static readonly JourneyCity Innsbruck = new("Innsbruck", "Austria", 47.2692, 11.4041);
    private static readonly JourneyCity Klagenfurt = new("Klagenfurt", "Austria", 46.6247, 14.3053);
    private static readonly JourneyCity Krakow = new("Kraków", "Poland", 50.0647, 19.9450);
    private static readonly JourneyCity Leipzig = new("Leipzig", "Germany", 51.3397, 12.3731);
    private static readonly JourneyCity Linz = new("Linz", "Austria", 48.3069, 14.2858);
    private static readonly JourneyCity Ljubljana = new("Ljubljana", "Slovenia", 46.0569, 14.5058);
    private static readonly JourneyCity Luxembourg = new("Luxembourg", "Luxembourg", 49.6116, 6.1319);
    private static readonly JourneyCity Maribor = new("Maribor", "Slovenia", 46.5547, 15.6459);
    private static readonly JourneyCity Milan = new("Milan", "Italy", 45.4642, 9.1900);
    private static readonly JourneyCity Munich = new("Munich", "Germany", 48.1351, 11.5820);
    private static readonly JourneyCity Nuremberg = new("Nuremberg", "Germany", 49.4521, 11.0767);
    private static readonly JourneyCity Olomouc = new("Olomouc", "Czechia", 49.5938, 17.2509);
    private static readonly JourneyCity Paris = new("Paris", "France", 48.8566, 2.3522);
    private static readonly JourneyCity Pecs = new("Pécs", "Hungary", 46.0727, 18.2323);
    private static readonly JourneyCity Prague = new("Prague", "Czechia", 50.0755, 14.4378);
    private static readonly JourneyCity Salzburg = new("Salzburg", "Austria", 47.8095, 13.0550);
    private static readonly JourneyCity Siofok = new("Siófok", "Hungary", 46.9041, 18.0490);
    private static readonly JourneyCity Sopron = new("Sopron", "Hungary", 47.6817, 16.5845);
    private static readonly JourneyCity Strasbourg = new("Strasbourg", "France", 48.5734, 7.7521);
    private static readonly JourneyCity Stuttgart = new("Stuttgart", "Germany", 48.7758, 9.1829);
    private static readonly JourneyCity Trnava = new("Trnava", "Slovakia", 48.3774, 17.5872);
    private static readonly JourneyCity Vaduz = new("Vaduz", "Liechtenstein", 47.1410, 9.5209);
    private static readonly JourneyCity Venice = new("Venice", "Italy", 45.4408, 12.3155);
    private static readonly JourneyCity Verona = new("Verona", "Italy", 45.4384, 10.9916);
    private static readonly JourneyCity Warsaw = new("Warsaw", "Poland", 52.2297, 21.0122);
    private static readonly JourneyCity WienerNeustadt = new("Wiener Neustadt", "Austria", 47.8150, 16.2433);
    private static readonly JourneyCity Wroclaw = new("Wrocław", "Poland", 51.1079, 17.0385);
    private static readonly JourneyCity Zagreb = new("Zagreb", "Croatia", 45.8150, 15.9819);
    private static readonly JourneyCity Znojmo = new("Znojmo", "Czechia", 48.8555, 16.0488);
    private static readonly JourneyCity Zurich = new("Zurich", "Switzerland", 47.3769, 8.5417);

    /// <summary>The capitals tour (about 2,062 km), for a year without a Yearly Goal.</summary>
    private static readonly JourneyTour Capitals = new(Vienna, Bratislava, Budapest, Zagreb, Ljubljana, Vaduz, Bern, Prague, Vienna);

    /// <summary>
    /// Every tour, shortest first, from about 194 km to about 2,970 km, close enough together that a Yearly Goal gets a
    /// tour of nearly its length. The comments give each tour's length in km, rounded.
    /// </summary>
    public static readonly IReadOnlyList<JourneyTour> Tours =
    [
        /*   194 */ new(Vienna, Bratislava, Sopron, WienerNeustadt, Vienna),
        /*   308 */ new(Vienna, Znojmo, Brno, Bratislava, Vienna),
        /*   411 */ new(Vienna, Brno, Olomouc, Trnava, Bratislava, Vienna),
        /*   463 */ new(Vienna, Bratislava, Budapest, Gyor, Sopron, Vienna),
        /*   499 */ new(Vienna, Linz, CeskeBudejovice, Brno, Vienna),
        /*   582 */ new(Vienna, Brno, Prague, CeskeBudejovice, Vienna),
        /*   659 */ new(Vienna, Bratislava, Budapest, Siofok, Graz, Vienna),
        /*   797 */ new(Vienna, Graz, Maribor, Ljubljana, Klagenfurt, Salzburg, Linz, Vienna),
        /*   853 */ new(Vienna, Bratislava, Budapest, Pecs, Zagreb, Graz, Vienna),
        /*   958 */ new(Vienna, Bratislava, Budapest, Pecs, Zagreb, Ljubljana, Graz, Vienna),
        /* 1,086 */ new(Vienna, Graz, Ljubljana, Venice, Bolzano, Innsbruck, Salzburg, Linz, Vienna),
        /* 1,202 */ new(Vienna, Brno, Prague, Dresden, Nuremberg, Munich, Salzburg, Linz, Vienna),
        /* 1,272 */ new(Vienna, Brno, Prague, Dresden, Leipzig, Nuremberg, Munich, Salzburg, Linz, Vienna),
        /* 1,430 */ new(Vienna, Bratislava, Budapest, Zagreb, Ljubljana, Venice, Innsbruck, Salzburg, Linz, Vienna),
        /* 1,524 */ new(Vienna, Brno, Krakow, Warsaw, Wroclaw, Dresden, Prague, Vienna),
        /* 1,590 */ new(Vienna, Bratislava, Budapest, Zagreb, Ljubljana, Venice, Verona, Bolzano, Innsbruck, Munich, Salzburg, Vienna),
        /* 1,680 */ new(Vienna, Graz, Ljubljana, Venice, Bologna, Verona, Milan, Zurich, Vaduz, Innsbruck, Salzburg, Linz, Vienna),
        /* 1,893 */ new(Vienna, Prague, Dresden, Berlin, Hamburg, Frankfurt, Stuttgart, Munich, Salzburg, Vienna),
        /* 2,062 */ Capitals,
        /* 2,203 */ new(Vienna, Brno, Krakow, Warsaw, Berlin, Dresden, Prague, Nuremberg, Munich, Salzburg, Linz, Vienna),
        /* 2,334 */ new(Vienna, Prague, Dresden, Berlin, Hamburg, Amsterdam, Brussels, Luxembourg, Strasbourg, Munich, Salzburg, Vienna),
        /* 2,651 */ new(Vienna, Prague, Berlin, Hamburg, Amsterdam, Brussels, Paris, Strasbourg, Stuttgart, Munich, Salzburg, Vienna),
        /* 2,970 */ new(Vienna, Bratislava, Budapest, Zagreb, Ljubljana, Vaduz, Bern, Luxembourg, Brussels, Amsterdam, Berlin, Prague, Vienna),
    ];

    /// <summary>
    /// The longest tour no longer than <paramref name="yearlyGoal"/> (metres), or the shortest if none is; the capitals
    /// tour without a goal.
    /// </summary>
    public static JourneyTour TourFor(double? yearlyGoal) =>
        yearlyGoal is not { } goal ? Capitals : Tours.LastOrDefault(t => t.Length <= goal) ?? Tours[0];

    /// <summary>The year's Runs, by local start, on the tour that fits the year's Yearly Goal.</summary>
    public static async Task<JourneyResponse> ForYearAsync(AppDbContext db, int year, CancellationToken cancellationToken)
    {
        var yearStart = new DateTime(year, 1, 1);
        var nextYearStart = yearStart.AddYears(1);
        var runs = await db.Runs
            .Where(r => r.StartLocal >= yearStart && r.StartLocal < nextYearStart)
            .Select(r => new JourneyRun(r.Id, r.Name, r.StartLocal, r.Distance))
            .ToListAsync(cancellationToken);
        var goal = await db.GetYearlyGoalAsync(year, cancellationToken);
        return JourneyResponse.Of(year, runs, goal?.Distance);
    }

    /// <summary>Metres between two points on the Earth's surface, along a great circle.</summary>
    public static double GreatCircle(JourneyCity from, JourneyCity to)
    {
        const double earthRadius = 6_371_008.8;
        var latitude1 = double.DegreesToRadians(from.Latitude);
        var latitude2 = double.DegreesToRadians(to.Latitude);
        var halfLatitude = (latitude2 - latitude1) / 2;
        var halfLongitude = double.DegreesToRadians(to.Longitude - from.Longitude) / 2;
        var a = Math.Sin(halfLatitude) * Math.Sin(halfLatitude)
            + Math.Cos(latitude1) * Math.Cos(latitude2) * Math.Sin(halfLongitude) * Math.Sin(halfLongitude);
        return 2 * earthRadius * Math.Asin(Math.Sqrt(a));
    }
}
