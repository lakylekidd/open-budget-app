using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenBudget.Api.Mcp;

/// <summary>Builds the JSON Schema subset used by OpenBudget tool input schemas.</summary>
public static class S
{
    public const decimal MaxMoney = 999_999_999.99m;

    public static JsonObject Str(string description, int maxLength = 200, int minLength = 0, string? pattern = null, string? format = null)
    {
        var schema = new JsonObject { ["type"] = "string", ["description"] = description, ["maxLength"] = maxLength };
        if (minLength > 0) schema["minLength"] = minLength;
        if (pattern is not null) schema["pattern"] = pattern;
        if (format is not null) schema["format"] = format;
        return schema;
    }

    public static JsonObject Enum(string description, params string[] values) => new()
    {
        ["type"] = "string", ["description"] = description, ["enum"] = new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray())
    };

    public static JsonObject Id(string description) => Str(description, 100, 1, "^[A-Za-z0-9_-]{1,100}$");
    public static JsonObject BudgetId() => Id("Budget ID from budgets_list.");
    public static JsonObject Uuid(string description) => Str(description, 36, 36, "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", "uuid");
    public static JsonObject Month(string description = "Plan month in YYYY-MM format.") => Str(description, 7, 7, "^[0-9]{4}-(0[1-9]|1[0-2])$");
    public static JsonObject Date(string description) => Str(description, 10, 10, "^[0-9]{4}-[0-9]{2}-[0-9]{2}$", "date");
    public static JsonObject Bool(string description) => new() { ["type"] = "boolean", ["description"] = description };

    public static JsonObject Int(string description, long minimum, long maximum) => new()
    {
        ["type"] = "integer", ["description"] = description, ["minimum"] = minimum, ["maximum"] = maximum
    };

    /// <summary>Money with at most two decimal places. Outflows are negative where the description says so.</summary>
    public static JsonObject Money(string description, decimal minimum = -MaxMoney, decimal maximum = MaxMoney) => new()
    {
        ["type"] = "number", ["description"] = description + " At most two decimal places.", ["minimum"] = minimum, ["maximum"] = maximum,
        ["multipleOf"] = 0.01
    };

    public static JsonObject Array(string description, JsonObject items, int maxItems, int minItems = 0) => new()
    {
        ["type"] = "array", ["description"] = description, ["items"] = items, ["minItems"] = minItems, ["maxItems"] = maxItems
    };

    public static JsonObject Nullable(JsonObject schema)
    {
        var type = schema["type"]!.GetValue<string>();
        schema["type"] = new JsonArray(type, "null");
        return schema;
    }

    public static JsonObject Obj(params (string Name, JsonObject Schema, bool Required)[] properties) => Obj(null, properties);

    public static JsonObject Obj(string? description, params (string Name, JsonObject Schema, bool Required)[] properties)
    {
        var props = new JsonObject();
        foreach (var property in properties) props[property.Name] = property.Schema;
        var schema = new JsonObject { ["type"] = "object", ["properties"] = props, ["additionalProperties"] = false };
        if (description is not null) schema["description"] = description;
        var required = properties.Where(x => x.Required).Select(x => (JsonNode?)JsonValue.Create(x.Name)).ToArray();
        if (required.Length > 0) schema["required"] = new JsonArray(required);
        return schema;
    }

    public static (string, JsonObject, bool) R(string name, JsonObject schema) => (name, schema, true);
    public static (string, JsonObject, bool) O(string name, JsonObject schema) => (name, schema, false);
}

/// <summary>
/// Validates tool arguments against the schema subset produced by <see cref="S"/>: object properties/required/
/// additionalProperties, string length/pattern/enum/format, integer and number bounds (money precision), booleans,
/// arrays and nullable types. Error messages name the argument path but never echo values.
/// </summary>
public static class McpArgumentValidator
{
    private const int MaxDepth = 8;

    public static string? Validate(JsonObject schema, JsonElement arguments) => Validate(schema, arguments, "arguments", 0);

    private static string? Validate(JsonNode schema, JsonElement value, string path, int depth)
    {
        if (depth > MaxDepth) return $"{path} is nested too deeply.";
        var types = schema["type"] switch
        {
            JsonArray array => array.Select(x => x!.GetValue<string>()).ToArray(),
            JsonValue single => [single.GetValue<string>()],
            _ => []
        };
        if (value.ValueKind == JsonValueKind.Null)
            return types.Contains("null") ? null : $"{path} must not be null.";
        var type = types.FirstOrDefault(x => x != "null");
        switch (type)
        {
            case "object":
                if (value.ValueKind != JsonValueKind.Object) return $"{path} must be an object.";
                var properties = schema["properties"] as JsonObject ?? [];
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    if (!seen.Add(property.Name)) return $"{path} contains a duplicate property.";
                    if (!properties.TryGetPropertyValue(property.Name, out var propertySchema) || propertySchema is null)
                    {
                        if (schema["additionalProperties"] is JsonValue additional && !additional.GetValue<bool>())
                            return $"{path} has an unknown property '{Truncate(property.Name)}'.";
                        continue;
                    }
                    var error = Validate(propertySchema, property.Value, $"{path}.{property.Name}", depth + 1);
                    if (error is not null) return error;
                }
                if (schema["required"] is JsonArray required)
                    foreach (var name in required.Select(x => x!.GetValue<string>()))
                        if (!seen.Contains(name)) return $"{path}.{name} is required.";
                return null;
            case "array":
                if (value.ValueKind != JsonValueKind.Array) return $"{path} must be an array.";
                var length = value.GetArrayLength();
                if (schema["minItems"] is JsonValue min && length < min.GetValue<int>()) return $"{path} must contain at least {min.GetValue<int>()} items.";
                if (schema["maxItems"] is JsonValue max && length > max.GetValue<int>()) return $"{path} must contain at most {max.GetValue<int>()} items.";
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    var error = schema["items"] is { } items ? Validate(items, item, $"{path}[{index}]", depth + 1) : null;
                    if (error is not null) return error;
                    index++;
                }
                return null;
            case "string":
                if (value.ValueKind != JsonValueKind.String) return $"{path} must be a string.";
                var text = value.GetString()!;
                if (schema["minLength"] is JsonValue minLength && text.Length < minLength.GetValue<int>()) return $"{path} must be at least {minLength.GetValue<int>()} characters.";
                if (schema["maxLength"] is JsonValue maxLength && text.Length > maxLength.GetValue<int>()) return $"{path} must be at most {maxLength.GetValue<int>()} characters.";
                if (text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))) return $"{path} contains control characters.";
                if (schema["enum"] is JsonArray allowed && allowed.All(x => x!.GetValue<string>() != text))
                    return $"{path} must be one of: {string.Join(", ", allowed.Select(x => x!.GetValue<string>()))}.";
                if (schema["pattern"] is JsonValue pattern && !Regex.IsMatch(text, pattern.GetValue<string>(), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                    return $"{path} has an invalid format.";
                if (schema["format"] is JsonValue format)
                {
                    var ok = format.GetValue<string>() switch
                    {
                        "uuid" => Guid.TryParseExact(text, "D", out _),
                        "date" => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                        _ => true
                    };
                    if (!ok) return $"{path} must be a valid {format.GetValue<string>()}.";
                }
                return null;
            case "integer":
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var integer)) return $"{path} must be an integer.";
                if (schema["minimum"] is JsonValue minimum && integer < minimum.GetValue<long>()) return $"{path} must be at least {minimum.GetValue<long>()}.";
                if (schema["maximum"] is JsonValue maximum && integer > maximum.GetValue<long>()) return $"{path} must be at most {maximum.GetValue<long>()}.";
                return null;
            case "number":
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number)) return $"{path} must be a number.";
                if (schema["minimum"] is JsonValue numberMin && number < numberMin.GetValue<decimal>()) return $"{path} is below the allowed minimum.";
                if (schema["maximum"] is JsonValue numberMax && number > numberMax.GetValue<decimal>()) return $"{path} is above the allowed maximum.";
                if (schema["multipleOf"] is not null && decimal.Round(number, 2) != number) return $"{path} must have at most two decimal places.";
                return null;
            case "boolean":
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : $"{path} must be true or false.";
            default:
                return null;
        }
    }

    private static string Truncate(string name) => name.Length <= 40 ? name : name[..40] + "…";
}
