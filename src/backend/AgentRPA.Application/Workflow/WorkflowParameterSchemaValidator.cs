using System.Text.Json;

namespace AgentRPA.Application.Workflow;

/// <summary>Workflow 参数 Schema 校验器。Schema 位于 definition.parameters，执行前由服务端再次校验。</summary>
public sealed class WorkflowParameterSchemaValidator
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "integer", "number", "boolean", "object", "array"
    };

    public IReadOnlyList<string> ValidateDefinition(JsonElement root)
    {
        if (!root.TryGetProperty("parameters", out var parameters)) return [];
        if (parameters.ValueKind != JsonValueKind.Object) return ["Workflow parameters 必须是 JSON Object。"];

        var errors = new List<string>();
        foreach (var property in parameters.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name) || property.Name.Length > 128)
            {
                errors.Add("Workflow 参数名称不能为空且不能超过 128 个字符。");
                continue;
            }
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"参数 {property.Name} 的 Schema 必须是 Object。");
                continue;
            }
            var schema = property.Value;
            if (!schema.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || !AllowedTypes.Contains(type.GetString() ?? string.Empty))
                errors.Add($"参数 {property.Name} 的 type 无效。");
            if (schema.TryGetProperty("required", out var required) && required.ValueKind != JsonValueKind.True && required.ValueKind != JsonValueKind.False)
                errors.Add($"参数 {property.Name} 的 required 必须是 Boolean。");
            if (schema.TryGetProperty("sensitive", out var sensitive) && sensitive.ValueKind != JsonValueKind.True && sensitive.ValueKind != JsonValueKind.False)
                errors.Add($"参数 {property.Name} 的 sensitive 必须是 Boolean。");
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
            if (value is null && required)
            {
                errors.Add($"缺少必填参数：{property.Name}。");
                continue;
            }
            if (value is null) continue;
            var type = schema.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            if (!MatchesType(type, value)) errors.Add($"参数 {property.Name} 类型不匹配，要求 {type}。");
        }
        return errors;
    }

    private static bool MatchesType(string? type, object value) => type?.ToLowerInvariant() switch
    {
        "string" => value is string,
        "integer" => value is sbyte or byte or short or ushort or int or uint or long or ulong,
        "number" => value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal,
        "boolean" => value is bool,
        "object" => value is JsonElement element && element.ValueKind == JsonValueKind.Object || value is IReadOnlyDictionary<string, object?>,
        "array" => value is JsonElement array && array.ValueKind == JsonValueKind.Array || value is System.Collections.IEnumerable && value is not string,
        _ => false
    };
}
