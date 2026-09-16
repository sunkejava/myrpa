using System.Text.Json;
using AgentRPA.Domain.Workflow;
namespace AgentRPA.Application.Workflow;

/// <summary>Workflow 发布前的确定性结构校验。</summary>
public sealed class WorkflowDefinitionValidator(WorkflowParameterSchemaValidator parameterSchemaValidator)
{
    public IReadOnlyList<string> Validate(string definitionJson)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return ["Workflow 根节点必须是 JSON Object。"];
            var errors = new List<string>(parameterSchemaValidator.ValidateDefinition(root));
            if (!root.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array) return [.. errors, "Workflow 必须包含 steps 数组。"];
            var index = 0;
            foreach (var step in steps.EnumerateArray())
            {
                index++;
                if (step.ValueKind != JsonValueKind.Object) { errors.Add($"第 {index} 个 Step 必须是 Object。"); continue; }
                if (!step.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String) { errors.Add($"第 {index} 个 Step 缺少 type。"); continue; }
                var type = typeElement.GetString() ?? string.Empty;
                if (!Enum.TryParse<WorkflowStepType>(type, true, out var stepType)) { errors.Add($"第 {index} 个 Step 类型不支持：{type}"); continue; }
                if (stepType == WorkflowStepType.Script) errors.Add($"第 {index} 个 Script Step 必须通过受控 Provider 执行。");
                if (stepType is WorkflowStepType.Navigate or WorkflowStepType.Click or WorkflowStepType.Input or WorkflowStepType.Select or WorkflowStepType.WaitForElement or WorkflowStepType.Extract or WorkflowStepType.Upload or WorkflowStepType.Assert)
                    if (!step.TryGetProperty("config", out var config) || config.ValueKind != JsonValueKind.Object) errors.Add($"第 {index} 个 {type} Step 缺少 config。");
            }
            if (steps.GetArrayLength() == 0) errors.Add("Workflow 至少需要一个 Step。");
            return errors;
        }
        catch (JsonException ex) { return [$"Workflow JSON 无效：{ex.Message}"]; }
    }
}
