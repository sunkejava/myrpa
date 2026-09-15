using System.Text.Json;
using System.Text.RegularExpressions;
using AgentRPA.Application.Scheduling;
using AgentRPA.Contracts.Tasks;

namespace AgentRPA.Application.Agent;

public sealed record AgentPlanResult(bool Success, TaskPlanDto? Plan, IReadOnlyList<string> Ambiguities, string Summary);

/// <summary>
/// Agent 规划器：优先使用确定性资源匹配；无法唯一识别资源时，可调用 LLM 生成结构化候选，
/// 但最终资源 ID、Workflow 和风险均必须由服务端目录与规则再次校验。
/// </summary>
public sealed class AgentPlanningService(
    IAgentResourceCatalog catalog,
    IAgentWorkflowResolver workflowResolver,
    ILlmProvider llmProvider)
{
    public async Task<AgentPlanResult> PlanAsync(string instruction, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return new(false, null, ["请输入自动化指令。"], "指令为空");
        var text = instruction.Trim();
        var cities = await catalog.GetCitiesAsync(cancellationToken);
        var systems = await catalog.GetSystemsAsync(cancellationToken);
        var functions = await catalog.GetFunctionsAsync(cancellationToken);

        var resolution = ResolveDeterministically(text, cities, systems, functions);
        if (!resolution.Success)
        {
            // LLM 仅负责语义消歧，最终仍必须通过服务端资源目录的 Code + 归属关系校验。
            var llmResolution = await ResolveWithLlmAsync(text, cities, systems, functions, cancellationToken);
            if (llmResolution is null)
                return new(false, null, resolution.Ambiguities, "需要补充业务资源信息后才能生成执行计划。");
            resolution = llmResolution;
        }

        var functionId = resolution.FunctionId;
        var workflows = await workflowResolver.ResolveAsync(functionId, cancellationToken);
        if (workflows.Count == 0) return new(false, null, ["该业务功能暂无已发布的可执行 Workflow。"], "无法绑定已发布 Workflow。");

        var defaults = workflows.Where(x => x.IsDefault).ToArray();
        AgentWorkflowResource workflow;
        if (defaults.Length == 1) workflow = defaults[0];
        else if (workflows.Select(x => x.WorkflowId).Distinct().Count() == 1) workflow = workflows[0];
        else return new(false, null, ["该业务功能存在多个已发布 Workflow，请配置且仅保留一个默认 Workflow 后再执行。"], "Workflow 选择存在歧义。");

        var action = resolution.Action;
        var risk = ResolveRisk(text, action);
        var parameters = resolution.Parameters;
        var plan = new TaskPlanDto(resolution.CityId, resolution.SystemId, functionId, action, parameters, [], workflow.WorkflowId.ToString(), workflow.Version, risk, risk != "Low");
        return new(true, plan, [], $"已解析为：{resolution.CityName} / {resolution.SystemName} / {resolution.FunctionName} / Workflow {workflow.Name} v{workflow.Version} / {action}。");
    }

    private static Resolution ResolveDeterministically(
        string text,
        IReadOnlyList<AgentCityResource> cities,
        IReadOnlyList<AgentSystemResource> systems,
        IReadOnlyList<AgentFunctionResource> functions)
    {
        var cityMatches = cities.Where(x => Contains(text, x.Name) || Contains(text, x.Code)).ToArray();
        if (cityMatches.Length != 1)
            return Resolution.Failed(cityMatches.Length == 0 ? "无法从指令识别城市。" : "指令匹配到多个城市，请明确城市。");

        var city = cityMatches[0];
        var systemMatches = systems.Where(x => x.CityId == city.Id && (Contains(text, x.Name) || Contains(text, x.Code))).ToArray();
        if (systemMatches.Length != 1)
            return Resolution.Failed(systemMatches.Length == 0 ? "无法识别业务系统。" : "指令匹配到多个业务系统，请明确系统。");

        var system = systemMatches[0];
        var functionMatches = functions.Where(x => x.SystemId == system.Id && (Contains(text, x.Name) || Contains(text, x.Code))).ToArray();
        if (functionMatches.Length != 1)
            return Resolution.Failed(functionMatches.Length == 0 ? "无法识别业务功能。" : "指令匹配到多个业务功能，请明确功能。");

        var function = functionMatches[0];
        return new(true, city.Id, system.Id, function.Id, city.Name, system.Name, function.Name, ResolveAction(text), ParseParameters(text), []);
    }

    private async Task<Resolution?> ResolveWithLlmAsync(
        string text,
        IReadOnlyList<AgentCityResource> cities,
        IReadOnlyList<AgentSystemResource> systems,
        IReadOnlyList<AgentFunctionResource> functions,
        CancellationToken cancellationToken)
    {
        // 只把必要的资源目录信息提供给模型，不向模型暴露凭据、Cookie 或执行节点信息。
        var catalogJson = JsonSerializer.Serialize(new
        {
            cities = cities.Select(x => new { x.Code, x.Name }),
            systems = systems.Select(x => new { x.Code, x.Name, cityCode = cities.FirstOrDefault(c => c.Id == x.CityId)?.Code }),
            functions = functions.Select(x => new { x.Code, x.Name, systemCode = systems.FirstOrDefault(s => s.Id == x.SystemId)?.Code })
        });

        const string systemPrompt = "你是企业 RPA 任务规划器。只能从给定资源目录中选择 cityCode、systemCode、functionCode，不得编造资源。输出严格 JSON，不要 Markdown。action 只能是 Execute、Export、Download、Upload、Delete、Create、Update。parameters 必须是 JSON 对象。";
        var userPrompt = $"资源目录：{catalogJson}\n用户指令：{text}\n请输出：{{\"cityCode\":\"\",\"systemCode\":\"\",\"functionCode\":\"\",\"action\":\"Execute\",\"parameters\":{{}}}}";
        var response = await llmProvider.CompleteAsync(new LlmRequest(systemPrompt, userPrompt, 0.0, 800), cancellationToken);
        if (!response.Success || string.IsNullOrWhiteSpace(response.Content)) return null;

        try
        {
            using var document = JsonDocument.Parse(ExtractJson(response.Content));
            var root = document.RootElement;
            var cityCode = GetRequiredString(root, "cityCode");
            var systemCode = GetRequiredString(root, "systemCode");
            var functionCode = GetRequiredString(root, "functionCode");
            var action = GetRequiredString(root, "action");
            if (!IsAllowedAction(action)) return null;

            var city = cities.SingleOrDefault(x => string.Equals(x.Code, cityCode, StringComparison.OrdinalIgnoreCase));
            if (city is null) return null;
            var system = systems.SingleOrDefault(x => x.CityId == city.Id && string.Equals(x.Code, systemCode, StringComparison.OrdinalIgnoreCase));
            if (system is null) return null;
            var function = functions.SingleOrDefault(x => x.SystemId == system.Id && string.Equals(x.Code, functionCode, StringComparison.OrdinalIgnoreCase));
            if (function is null) return null;

            var parameters = ParseParameters(text);
            if (root.TryGetProperty("parameters", out var parameterElement) && parameterElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in parameterElement.EnumerateObject())
                    parameters[property.Name] = property.Value.Clone();
            }

            return new(true, city.Id, system.Id, function.Id, city.Name, system.Name, function.Name, action, parameters, []);
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static Dictionary<string, object?> ParseParameters(string text)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["instruction"] = text };
        var match = Regex.Match(text, "(?:文件|Excel|表格)[:：]?[\\s]*([^，。；;\\s]+\\.(?:xlsx|xls|csv))", RegexOptions.IgnoreCase);
        if (match.Success) parameters["fileName"] = match.Groups[1].Value;
        return parameters;
    }

    private static string ExtractJson(string content)
    {
        var value = content.Trim();
        if (value.StartsWith("```") && value.EndsWith("```"))
        {
            var firstLine = value.IndexOf('\n');
            value = firstLine >= 0 ? value[(firstLine + 1)..^3].Trim() : value.Trim('`');
        }
        var start = value.IndexOf('{');
        var end = value.LastIndexOf('}');
        return start >= 0 && end > start ? value[start..(end + 1)] : value;
    }

    private static string GetRequiredString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static bool IsAllowedAction(string action) => action is "Execute" or "Export" or "Download" or "Upload" or "Delete" or "Create" or "Update";
    private static bool Contains(string text, string value) => !string.IsNullOrWhiteSpace(value) && text.Contains(value, StringComparison.OrdinalIgnoreCase);
    private static string ResolveAction(string text) => text.Contains("导出", StringComparison.OrdinalIgnoreCase) ? "Export" : text.Contains("下载", StringComparison.OrdinalIgnoreCase) ? "Download" : text.Contains("上传", StringComparison.OrdinalIgnoreCase) ? "Upload" : text.Contains("删除", StringComparison.OrdinalIgnoreCase) ? "Delete" : text.Contains("新增", StringComparison.OrdinalIgnoreCase) || text.Contains("添加", StringComparison.OrdinalIgnoreCase) ? "Create" : text.Contains("修改", StringComparison.OrdinalIgnoreCase) || text.Contains("更新", StringComparison.OrdinalIgnoreCase) ? "Update" : "Execute";
    private static string ResolveRisk(string text, string action) => action == "Delete" || text.Contains("注销", StringComparison.OrdinalIgnoreCase) ? "High" : action is "Create" or "Update" || text.Contains("提交", StringComparison.OrdinalIgnoreCase) ? "Medium" : "Low";

    private sealed record Resolution(bool Success, Guid CityId, Guid SystemId, Guid FunctionId, string CityName, string SystemName, string FunctionName, string Action, Dictionary<string, object?> Parameters, IReadOnlyList<string> Ambiguities)
    {
        public static Resolution Failed(string reason) => new(false, Guid.Empty, Guid.Empty, Guid.Empty, string.Empty, string.Empty, string.Empty, "Execute", [], [reason]);
    }
}
