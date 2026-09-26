using System.Text.Json;
using AgentRPA.Domain.Workflow;
using AgentRPA.Application.Permission;
namespace AgentRPA.Application.Workflow;

/// <summary>Workflow 发布前的确定性结构校验。</summary>
public sealed class WorkflowDefinitionValidator(WorkflowParameterSchemaValidator parameterSchemaValidator)
{
    /// <summary>兼容单元测试及非 DI 调用场景。</summary>
    public WorkflowDefinitionValidator() : this(new WorkflowParameterSchemaValidator()) { }

    public IReadOnlyList<string> Validate(string definitionJson)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return ["Workflow 根节点必须是 JSON Object。"];
            var errors = new List<string>(parameterSchemaValidator.ValidateDefinition(root));
            if (root.TryGetProperty("adapter", out var adapter))
            {
                var code = adapter.ValueKind == JsonValueKind.String ? adapter.GetString() : null;
                if (string.IsNullOrWhiteSpace(code) || code.Length > 64 || code.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
                    errors.Add("adapter 必须是长度不超过 64 的字母、数字或连字符编码。");
                else if (!string.Equals(code, "direct", StringComparison.OrdinalIgnoreCase) &&
                    (!root.TryGetProperty("executionRequirement", out var requirement) || requirement.ValueKind != JsonValueKind.Object ||
                     !requirement.TryGetProperty("requiredCapabilities", out var capabilities) || capabilities.ValueKind != JsonValueKind.Array ||
                     !capabilities.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String &&
                         string.Equals(x.GetString(), "Adapter:" + code, StringComparison.OrdinalIgnoreCase))))
                    errors.Add($"adapter {code} 必须声明 executionRequirement.requiredCapabilities 中的 Adapter:{code}。");
            }
            if (root.TryGetProperty("requiresApproval", out var rootApproval) && rootApproval.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                errors.Add("requiresApproval 必须是布尔值。");
            if (root.TryGetProperty("riskLevel", out var risk) &&
                (risk.ValueKind != JsonValueKind.String || !new[] { "Low", "Medium", "High", "Critical" }.Contains(risk.GetString(), StringComparer.OrdinalIgnoreCase)))
                errors.Add("riskLevel 必须是 Low、Medium、High 或 Critical。");
            if (!root.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array) return [.. errors, "Workflow 必须包含 steps 数组。"];
            ValidateSteps(steps, errors, "", 0);
            return errors;
        }
        catch (JsonException ex) { return [$"Workflow JSON 无效：{ex.Message}"]; }
    }

    private static void ValidateSteps(JsonElement steps, List<string> errors, string path, int depth)
    {
        if (depth > 8 || steps.GetArrayLength() > 1000) { errors.Add("Workflow 嵌套深度或步骤数超过限制。"); return; }
        var index = 0;
        foreach (var step in steps.EnumerateArray())
        {
            index++;
            if (step.ValueKind != JsonValueKind.Object) { errors.Add($"第 {index} 个 Step 必须是 Object。"); continue; }
            var location = $"{path}Step {index}";
            if (!step.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String) { errors.Add($"第 {index} 个 Step 缺少 type。"); continue; }
            var type = typeElement.GetString() ?? string.Empty;
            if (!Enum.TryParse<WorkflowStepType>(type, true, out var stepType)) { errors.Add($"第 {index} 个 Step 类型不支持：{type}"); continue; }
            if (stepType == WorkflowStepType.Script) errors.Add($"第 {index} 个 Script Step 必须通过受控 Provider 执行。");
            if (step.TryGetProperty("requiresApproval", out var approval) && approval.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                errors.Add($"{location} 的 requiresApproval 必须是布尔值。");
            if (step.TryGetProperty("timeoutMs", out var timeout) &&
                (timeout.ValueKind != JsonValueKind.Number || !timeout.TryGetInt32(out var timeoutValue) || timeoutValue is < 100 or > 120_000))
                errors.Add($"{location} 的 timeoutMs 必须在 100 至 120000 毫秒之间。");
            if (step.TryGetProperty("retryCount", out var retry) &&
                (retry.ValueKind != JsonValueKind.Number || !retry.TryGetInt32(out var retryValue) || retryValue is < 0 or > 3 ||
                 retryValue > 0 && stepType is not (WorkflowStepType.Navigate or WorkflowStepType.WaitForElement or WorkflowStepType.Assert or WorkflowStepType.Extract)))
                errors.Add($"{location} 的 retryCount 仅支持 Navigate、WaitForElement、Assert、Extract，范围为 0 至 3。");
            if (step.TryGetProperty("requiredAction", out var requiredAction) &&
                (requiredAction.ValueKind != JsonValueKind.String || !WorkflowPermissionPreflight.IsAllowedAction(requiredAction.GetString() ?? string.Empty)))
                errors.Add($"{location} 的 requiredAction 无效。");
            var hasConfig = step.TryGetProperty("config", out var config) && config.ValueKind == JsonValueKind.Object;
            if (stepType is WorkflowStepType.Navigate or WorkflowStepType.Click or WorkflowStepType.Input or WorkflowStepType.Select or WorkflowStepType.WaitForElement or WorkflowStepType.Extract or WorkflowStepType.Upload or WorkflowStepType.Assert)
                if (!hasConfig) errors.Add($"第 {index} 个 {type} Step 缺少 config。");
            if ((stepType is WorkflowStepType.Condition or WorkflowStepType.Loop or WorkflowStepType.SubWorkflow) && !hasConfig)
                errors.Add($"{location} 缺少嵌套 config。");
            if (!hasConfig) continue;
            if (stepType == WorkflowStepType.Navigate && !HasString(config, "url"))
                errors.Add($"{location} 缺少 config.url。");
            if (stepType is WorkflowStepType.Click or WorkflowStepType.Input or WorkflowStepType.Select or
                WorkflowStepType.WaitForElement or WorkflowStepType.Extract or WorkflowStepType.Upload or
                WorkflowStepType.Download or WorkflowStepType.Assert or WorkflowStepType.Condition && !HasString(config, "selector"))
                errors.Add($"{location} 缺少 config.selector。");
            if (stepType == WorkflowStepType.Upload && !HasString(config, "path"))
                errors.Add($"{location} 缺少 config.path。");
            if (stepType == WorkflowStepType.Extract &&
                (!HasString(config, "output") || config.GetProperty("output").GetString() is not { } output ||
                 output.Length > 64 || !char.IsLetter(output[0]) || output.Any(c => !char.IsLetterOrDigit(c) && c != '_')))
                errors.Add($"{location} 的 config.output 必须是以字母开头、最多 64 位的字段名。");
            if (config.TryGetProperty("selector", out var selector) && selector.ValueKind == JsonValueKind.String &&
                selector.GetString() is { } value && value.Contains("replace-", StringComparison.OrdinalIgnoreCase))
                errors.Add($"{location} 的占位选择器尚未替换。");
            if (stepType is WorkflowStepType.Loop or WorkflowStepType.SubWorkflow &&
                (!config.TryGetProperty("steps", out var nestedSteps) || nestedSteps.ValueKind != JsonValueKind.Array))
                errors.Add($"{location} 缺少 config.steps 步骤数组。");
            foreach (var key in new[] { "steps", "then", "else" })
            {
                if (!config.TryGetProperty(key, out var nested)) continue;
                if (nested.ValueKind != JsonValueKind.Array) { errors.Add($"{location}.{key} 必须是 Step 数组。"); continue; }
                ValidateSteps(nested, errors, $"{location}.{key}.", depth + 1);
            }
        }
        if (steps.GetArrayLength() == 0) errors.Add("Workflow 至少需要一个 Step。");
    }
    private static bool HasString(JsonElement value, string key) =>
        value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(field.GetString());
}
