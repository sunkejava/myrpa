using System.Text.Json;

namespace AgentRPA.Application.Scheduling;

/// <summary>解析 WorkflowDefinition 中可选的 executionRequirement 节点。</summary>
public static class WorkflowExecutionRequirementParser
{
    /// <summary>
    /// 支持的 JSON 结构：executionRequirement.osPlatforms/nodeKinds/browsers/requiredCapabilities/
    /// forbiddenCapabilities/networkZone/nodePoolId/requiredNodeIds/excludedNodeIds/requiredHardwareIds/requiresDesktopUi。
    /// 缺失字段表示不额外收紧上层 BusinessSystem 要求。
    /// </summary>
    public static ExecutionRequirement? Parse(string? definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson)) return null;
        using var document = JsonDocument.Parse(definitionJson);
        if (!document.RootElement.TryGetProperty("executionRequirement", out var root) || root.ValueKind != JsonValueKind.Object)
            return null;

        return new ExecutionRequirement(
            ReadStrings(root, "osPlatforms"),
            ReadStrings(root, "nodeKinds"),
            ReadStrings(root, "browsers"),
            ReadStrings(root, "requiredCapabilities"),
            ReadStrings(root, "forbiddenCapabilities"),
            ReadString(root, "networkZone"),
            ReadGuid(root, "nodePoolId"),
            ReadGuids(root, "requiredNodeIds"),
            ReadGuids(root, "excludedNodeIds"),
            ReadStrings(root, "requiredHardwareIds"),
            ReadBool(root, "requiresDesktopUi"),
            ReadString(root, "executionAffinity") ?? "Item");
    }

    private static IReadOnlySet<string> ReadStrings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlySet<Guid> ReadGuids(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return new HashSet<Guid>();
        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String && Guid.TryParse(x.GetString(), out _))
            .Select(x => Guid.Parse(x.GetString()!))
            .ToHashSet();
    }

    private static Guid? ReadGuid(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id)
            ? id
            : null;

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
