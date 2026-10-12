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
            if (Parse<TEnum>(name) is not { } value)
                return null;
            values.Add(value);
        }

        return values;
    }

    /// <summary>The value named; null if there is no name or it is not a value's.</summary>
    public static TEnum? Parse<TEnum>(string? name) where TEnum : struct, Enum =>
        // Names only: Enum.TryParse would also take "7".
        name is not null && Enum.GetNames<TEnum>().Contains(name, StringComparer.OrdinalIgnoreCase)
            ? Enum.Parse<TEnum>(name, ignoreCase: true)
            : null;
}
