using System.Text.Json;

namespace AgentRPA.Application.Workflow;

/// <summary>只根据服务端已发布 Workflow 的声明判断审批要求，不信任客户端传入的风险标记。</summary>
public static class WorkflowApprovalPolicy
{
    public static bool RequiresApproval(string definitionJson)
    {
        using var document = JsonDocument.Parse(definitionJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return false;
        if (root.TryGetProperty("requiresApproval", out var required) && required.ValueKind == JsonValueKind.True) return true;
        if (root.TryGetProperty("riskLevel", out var risk) && risk.ValueKind == JsonValueKind.String &&
            risk.GetString() is { } level && (level.Equals("High", StringComparison.OrdinalIgnoreCase) ||
                level.Equals("Critical", StringComparison.OrdinalIgnoreCase))) return true;
        return root.TryGetProperty("steps", out var steps) && ContainsApprovalStep(steps, 0);
    }

    private static bool ContainsApprovalStep(JsonElement steps, int depth)
    {
        if (depth > 8 || steps.ValueKind != JsonValueKind.Array) return false;
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object) continue;
            if (step.TryGetProperty("requiresApproval", out var required) && required.ValueKind == JsonValueKind.True) return true;
            if (step.TryGetProperty("requiredAction", out var action) && action.ValueKind == JsonValueKind.String &&
                string.Equals(action.GetString(), "Approve", StringComparison.OrdinalIgnoreCase)) return true;
            if (!step.TryGetProperty("config", out var config) || config.ValueKind != JsonValueKind.Object) continue;
            foreach (var key in new[] { "steps", "then", "else" })
                if (config.TryGetProperty(key, out var nested) && ContainsApprovalStep(nested, depth + 1)) return true;
        }
        return false;
    }
}
