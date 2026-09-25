using System.Text.Json;

namespace AgentRPA.Application.Workflow;

/// <summary>只允许确定为只读且无需审批的 Workflow 自动重试；无法识别时按不安全处理。</summary>
public static class WorkflowRetrySafety
{
    private static readonly HashSet<string> ReadOnlySteps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Navigate", "Wait", "WaitForElement", "Assert", "Extract", "Screenshot", "End"
    };

    public static bool IsSafeToRetry(string definitionJson)
    {
        try
        {
            if (WorkflowApprovalPolicy.RequiresApproval(definitionJson)) return false;
            using var doc = JsonDocument.Parse(definitionJson);
            var root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("steps", out var steps) && CheckSteps(steps, 0);
        }
        catch (JsonException) { return false; }
    }

    private static bool CheckSteps(JsonElement steps, int depth)
    {
        if (depth > 8 || steps.ValueKind != JsonValueKind.Array || steps.GetArrayLength() == 0) return false;
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object || !step.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
                return false;
            if (ReadOnlySteps.Contains(type.GetString() ?? "")) continue;
            if (type.GetString() is not { } name ||
                !new[] { "Condition", "Loop", "SubWorkflow" }.Contains(name, StringComparer.OrdinalIgnoreCase) ||
                !step.TryGetProperty("config", out var config) || config.ValueKind != JsonValueKind.Object) return false;
            var found = false;
            foreach (var key in new[] { "steps", "then", "else" })
            {
                if (!config.TryGetProperty(key, out var nested)) continue;
                found = true;
                if (!CheckSteps(nested, depth + 1)) return false;
            }
            if (!found) return false;
        }
        return true;
    }
}
