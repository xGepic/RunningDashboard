namespace RunningDashboard.Api;

/// <summary>The ?year= of a request for one calendar year, e.g. the Year in Review's.</summary>
public static class YearParameter
{
    /// <summary>Problem details for a year out of range; null if it is fine or not given.</summary>
    public static IResult? Reject(int? year) =>
        year is < 1900 or > 2099
            ? Results.ValidationProblem(new Dictionary<string, string[]> { ["year"] = ["The year is between 1900 and 2099."] })
            : null;
}
