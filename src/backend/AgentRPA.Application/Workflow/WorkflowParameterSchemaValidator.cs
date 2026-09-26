using System.Text.Json;
using System.Text.RegularExpressions;

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
            if (schema.TryGetProperty("minLength", out var minLength) && (minLength.ValueKind != JsonValueKind.Number || !minLength.TryGetInt32(out var min) || min < 0)) errors.Add($"参数 {property.Name} 的 minLength 无效。");
            if (schema.TryGetProperty("maxLength", out var maxLength) && (maxLength.ValueKind != JsonValueKind.Number || !maxLength.TryGetInt32(out var max) || max < 0)) errors.Add($"参数 {property.Name} 的 maxLength 无效。");
            if (TryInt(schema, "minLength", out var lowerLength) && TryInt(schema, "maxLength", out var upperLength) && lowerLength > upperLength)
                errors.Add($"参数 {property.Name} 的 minLength 不能大于 maxLength。");
            if (schema.TryGetProperty("minimum", out var minimum) && (minimum.ValueKind != JsonValueKind.Number || !minimum.TryGetDecimal(out _))) errors.Add($"参数 {property.Name} 的 minimum 必须是有效的 Number。");
            if (schema.TryGetProperty("maximum", out var maximum) && (maximum.ValueKind != JsonValueKind.Number || !maximum.TryGetDecimal(out _))) errors.Add($"参数 {property.Name} 的 maximum 必须是有效的 Number。");
            if (TryDecimal(schema, "minimum", out var lower) && TryDecimal(schema, "maximum", out var upper) && lower > upper)
                errors.Add($"参数 {property.Name} 的 minimum 不能大于 maximum。");
            if (schema.TryGetProperty("enum", out var enumValues) &&
                (enumValues.ValueKind != JsonValueKind.Array || enumValues.GetArrayLength() is 0 or > 100 ||
                 enumValues.EnumerateArray().Any(x => !MatchesJsonType(type, x))))
                errors.Add($"参数 {property.Name} 的 enum 必须包含 1 至 100 个与 type 一致的值。");
            if (schema.TryGetProperty("pattern", out var pattern))
            {
                if (!string.Equals(type, "string", StringComparison.OrdinalIgnoreCase) || pattern.ValueKind != JsonValueKind.String ||
                    pattern.GetString() is not { Length: > 0 and <= 256 } expression || !IsValidPattern(expression))
                    errors.Add($"参数 {property.Name} 的 pattern 必须是有效且不超过 256 字符的字符串正则表达式。");
            }
            if (schema.TryGetProperty("default", out var defaultValue) && !MatchesJsonType(type, defaultValue)) errors.Add($"参数 {property.Name} 的 default 与 type 不匹配。");
            else if (schema.TryGetProperty("default", out defaultValue) && MatchesJsonType(type, defaultValue))
            {
                var defaults = new List<string>();
                ValidateConstraints(property.Name, schema, defaultValue.ValueKind == JsonValueKind.String ? defaultValue.GetString()! :
                    defaultValue.ValueKind == JsonValueKind.Number && defaultValue.TryGetDecimal(out var numberDefault) ? numberDefault :
                    defaultValue.ValueKind is JsonValueKind.True or JsonValueKind.False ? defaultValue.GetBoolean() : defaultValue, defaults);
                if (defaults.Count > 0) errors.Add($"参数 {property.Name} 的 default 不满足约束。");
            }
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
            values.TryGetValue(property.Name, out var value);
            if (value is JsonElement element)
                value = element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
                    JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => element
                };
            if (value is null)
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
            if (schema.TryGetProperty("pattern", out var pattern) && pattern.ValueKind == JsonValueKind.String &&
                pattern.GetString() is { Length: > 0 and <= 256 } expression)
            {
                try { if (!Regex.IsMatch(text, expression, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) errors.Add($"参数 {name} 格式不匹配。"); }
                catch (RegexMatchTimeoutException) { errors.Add($"参数 {name} 格式检查超时。"); }
                catch (ArgumentException) { errors.Add($"参数 {name} 格式规则无效。"); }
            }
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

    private static bool IsValidPattern(string expression)
    {
        try { _ = new Regex(expression, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)); return true; }
        catch (ArgumentException) { return false; }
    }
}
