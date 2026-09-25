using System.Text.Json;

namespace AgentRPA.Application.Workflow;

/// <summary>Workflow 参数 Schema 校验器。Schema 位于 definition.parameters，创建/发布和执行前均由服务端校验。</summary>
public sealed class WorkflowParameterSchemaValidator
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase) { "string", "integer", "number", "boolean", "object", "array" };

    public IReadOnlyList<string> ValidateDefinition(JsonElement root)
    {
        if (!root.TryGetProperty("parameters", out var parameters)) return [];
        if (parameters.ValueKind != JsonValueKind.Object) return ["Workflow parameters 必须是 JSON Object。"];
        var errors = new List<string>();
        foreach (var property in parameters.EnumerateObject())
        {
            if (property.Name.Length is 0 or > 128) { errors.Add($"参数名称 {property.Name} 无效。"); continue; }
            if (property.Value.ValueKind != JsonValueKind.Object) { errors.Add($"参数 {property.Name} 的 Schema 必须是 Object。"); continue; }
            var schema = property.Value;
            var type = schema.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String ? typeElement.GetString() : null;
            if (!AllowedTypes.Contains(type ?? string.Empty)) errors.Add($"参数 {property.Name} 的 type 无效。");
            if (schema.TryGetProperty("required", out var required) && required.ValueKind is not JsonValueKind.True and not JsonValueKind.False) errors.Add($"参数 {property.Name} 的 required 必须是 Boolean。");
            if (schema.TryGetProperty("sensitive", out var sensitive) && sensitive.ValueKind is not JsonValueKind.True and not JsonValueKind.False) errors.Add($"参数 {property.Name} 的 sensitive 必须是 Boolean。");
            if (schema.TryGetProperty("minLength", out var minLength) && (!minLength.TryGetInt32(out var min) || min < 0)) errors.Add($"参数 {property.Name} 的 minLength 无效。");
            if (schema.TryGetProperty("maxLength", out var maxLength) && (!maxLength.TryGetInt32(out var max) || max < 0)) errors.Add($"参数 {property.Name} 的 maxLength 无效。");
            if (schema.TryGetProperty("minimum", out var minimum) && minimum.ValueKind is not JsonValueKind.Number) errors.Add($"参数 {property.Name} 的 minimum 必须是 Number。");
            if (schema.TryGetProperty("maximum", out var maximum) && maximum.ValueKind is not JsonValueKind.Number) errors.Add($"参数 {property.Name} 的 maximum 必须是 Number。");
            if (schema.TryGetProperty("enum", out var enumValues) && enumValues.ValueKind != JsonValueKind.Array) errors.Add($"参数 {property.Name} 的 enum 必须是 Array。");
            if (schema.TryGetProperty("default", out var defaultValue) && !MatchesJsonType(type, defaultValue)) errors.Add($"参数 {property.Name} 的 default 与 type 不匹配。");
        }
        return errors;
    }

    public IReadOnlyList<string> ValidateParameters(JsonElement root, IReadOnlyDictionary<string, object?> values)
    {
        if (!root.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Object) return [];
        var errors = new List<string>();
        foreach (var property in parameters.EnumerateObject())
        {
            var schema = property.Value;
            var required = schema.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.True;
            if (!values.TryGetValue(property.Name, out var value) || value is null)
            {
                if (required && !schema.TryGetProperty("default", out _)) errors.Add($"缺少必填参数：{property.Name}。");
                continue;
            }
            var type = schema.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            if (!MatchesType(type, value)) { errors.Add($"参数 {property.Name} 类型不匹配，要求 {type}。"); continue; }
            ValidateConstraints(property.Name, schema, value, errors);
        }
        return errors;
    }

    private static void ValidateConstraints(string name, JsonElement schema, object value, List<string> errors)
    {
        if (value is string text)
        {
            if (TryInt(schema, "minLength", out var min) && text.Length < min) errors.Add($"参数 {name} 长度不能小于 {min}。");
            if (TryInt(schema, "maxLength", out var max) && text.Length > max) errors.Add($"参数 {name} 长度不能超过 {max}。");
        }
        if (TryDecimal(value, out var number))
        {
            if (TryDecimal(schema, "minimum", out var minimum) && number < minimum) errors.Add($"参数 {name} 不能小于 {minimum}。");
            if (TryDecimal(schema, "maximum", out var maximum) && number > maximum) errors.Add($"参数 {name} 不能大于 {maximum}。");
        }
        if (schema.TryGetProperty("enum", out var enumValues) && enumValues.ValueKind == JsonValueKind.Array)
        {
            var actual = JsonSerializer.SerializeToElement(value);
            if (!enumValues.EnumerateArray().Any(x => x.GetRawText() == actual.GetRawText())) errors.Add($"参数 {name} 不在允许的枚举值范围内。");
        }
    }

    private static bool MatchesType(string? type, object value) => type?.ToLowerInvariant() switch
    {
        "string" => value is string,
        "integer" => value is sbyte or byte or short or ushort or int or uint or long or ulong,
        "number" => TryDecimal(value, out _),
        "boolean" => value is bool,
        "object" => value is JsonElement element && element.ValueKind == JsonValueKind.Object || value is IReadOnlyDictionary<string, object?>,
        "array" => value is JsonElement array && array.ValueKind == JsonValueKind.Array || value is System.Collections.IEnumerable && value is not string,
        _ => false
    };

    private static bool MatchesJsonType(string? type, JsonElement value) => type?.ToLowerInvariant() switch
    {
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        _ => false
    };

    private static bool TryInt(JsonElement root, string name, out int value)
    {
        value = default;
        return root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out value) && value >= 0;
    }

    private static bool TryDecimal(JsonElement root, string name, out decimal value)
    {
        value = default;
        return root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetDecimal(out value);
    }
    private static bool TryDecimal(object value, out decimal result)
    {
        switch (value) { case decimal d: result = d; return true; case double d when !double.IsNaN(d) && !double.IsInfinity(d): result = (decimal)d; return true; case float f when !float.IsNaN(f) && !float.IsInfinity(f): result = (decimal)f; return true; case byte b: result = b; return true; case sbyte b: result = b; return true; case short b: result = b; return true; case ushort b: result = b; return true; case int b: result = b; return true; case uint b: result = b; return true; case long b: result = b; return true; case ulong b: result = b; return true; default: result = 0; return false; }
    }
}
