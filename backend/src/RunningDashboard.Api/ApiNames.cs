using System.Text.Json;

namespace RunningDashboard.Api;

/// <summary>Enum values as the API names them: "build", "monday", "past".</summary>
public static class ApiNames
{
    public static string Of<TEnum>(TEnum value) where TEnum : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    /// <summary>The values named; null if one name is not a value's.</summary>
    public static List<TEnum>? Parse<TEnum>(IEnumerable<string> names) where TEnum : struct, Enum
    {
        var values = new List<TEnum>();
        foreach (var name in names)
        {
            // Names only: Enum.TryParse would also take "7".
            if (!Enum.GetNames<TEnum>().Contains(name, StringComparer.OrdinalIgnoreCase))
                return null;
            values.Add(Enum.Parse<TEnum>(name, ignoreCase: true));
        }

        return values;
    }
}
