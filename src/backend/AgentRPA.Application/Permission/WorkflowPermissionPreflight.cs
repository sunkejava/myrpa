using System.Text.Json;
using AgentRPA.Domain.Permission;

namespace AgentRPA.Application.Permission;

/// <summary>执行 Workflow 前遍历所有 Step（包括分支、循环和子流程），逐项验证资源动作。</summary>
public sealed class WorkflowPermissionPreflight(PermissionService permissions)
{
    public async Task<PermissionCheckResult> CheckAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId,
        string definitionJson, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(definitionJson);
            var actions = new List<(string Step, string Action)>();
            if (!doc.RootElement.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array ||
                !Collect(steps, "steps", 0, actions) || actions.Count == 0)
                return new(false, "Workflow Step 定义无效，拒绝执行。");
            var evaluated = new Dictionary<string, PermissionCheckResult>(StringComparer.Ordinal);
            foreach (var (step, action) in actions)
            {
                if (!evaluated.TryGetValue(action, out var required))
                {
                    required = await permissions.CheckAsync(subjectId, cityId, systemId, functionId, action, cancellationToken);
                    evaluated[action] = required;
                }
                if (!required.Allowed) return new(false, $"{step} 需要 {action} 权限：{required.Reason}");
            }
            return new(true, "Workflow 所有 Step 的权限预检通过。");
        }
        catch (JsonException) { return new(false, "Workflow 定义不是有效 JSON，拒绝执行。"); }
        catch (InvalidOperationException) { return new(false, "Workflow Step 定义无效，拒绝执行。"); }
    }

    private static bool Collect(JsonElement steps, string path, int depth, List<(string Step, string Action)> result)
    {
        if (depth > 8 || steps.ValueKind != JsonValueKind.Array || steps.GetArrayLength() > 1000) return false;
        var number = 0;
        foreach (var step in steps.EnumerateArray())
        {
            number++;
            if (step.ValueKind != JsonValueKind.Object || result.Count >= 1000 ||
                !step.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) return false;
            var name = $"{path}[{number}]";
            var action = "Execute";
            if (step.TryGetProperty("requiredAction", out var requiredAction))
            {
                if (requiredAction.ValueKind != JsonValueKind.String ||
                    requiredAction.GetString() is not { } value || !IsAllowedAction(value)) return false;
                action = value;
            }
            // 执行权是每个 Step 的基础门禁，附加的业务动作只能进一步收紧权限。
            result.Add((name, "Execute"));
            if (action != "Execute") result.Add((name, action));
            if (!step.TryGetProperty("config", out var config) || config.ValueKind != JsonValueKind.Object) continue;
            foreach (var nestedName in new[] { "steps", "then", "else" })
            {
                if (!config.TryGetProperty(nestedName, out var nested)) continue;
                if (!Collect(nested, $"{name}.{nestedName}", depth + 1, result)) return false;
            }
        }
        return true;
    }

    public static bool IsAllowedAction(string value) => value is "View" or "Execute" or "Create" or "Update" or "Delete" or "Approve" or "Manage" or "Export" or "Download" or "Upload";
}
