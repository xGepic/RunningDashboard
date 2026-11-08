namespace RunningDashboard.Api.Tests.Harness;

/// <summary>The body of a 400 for a request with invalid fields.</summary>
/// <param name="Errors">The messages for each invalid field, by its JSON name.</param>
public sealed record ValidationProblem(string Title, Dictionary<string, string[]> Errors);
