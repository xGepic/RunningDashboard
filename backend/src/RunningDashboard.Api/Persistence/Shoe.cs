using RunningDashboard.Api.Strava;

namespace RunningDashboard.Api.Persistence;

/// <summary>
/// A pair of running shoes registered on Strava. Runs refer to it by its gear id (<see cref="Run.ShoeId"/>).
/// </summary>
public sealed class Shoe
{
    /// <summary>Strava's gear id, e.g. "g31444377".</summary>
    public required string Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Metres, as Strava counts them (including any distance entered there by hand).</summary>
    public double Distance { get; set; }

    public bool Retired { get; set; }

    public required string RawJson { get; set; }

    public static Shoe From(StravaShoe shoe) => new() { Id = shoe.Id, Name = shoe.Name, Distance = shoe.Distance, Retired = shoe.Retired, RawJson = shoe.RawJson };

    public void UpdateFrom(StravaShoe shoe)
    {
        Name = shoe.Name;
        Distance = shoe.Distance;
        Retired = shoe.Retired;
        RawJson = shoe.RawJson;
    }
}
