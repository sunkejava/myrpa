using AgentRPA.Application.Scheduling;
using AgentRPA.Contracts.Tasks;

namespace AgentRPA.Application.Agent;

public sealed record AgentPlanResult(bool Success, TaskPlanDto? Plan, IReadOnlyList<string> Ambiguities, string Summary);

/// <summary>Agent 规划器：解析资源、动作、风险，并绑定唯一已发布 Workflow。</summary>
public sealed class AgentPlanningService(IAgentResourceCatalog catalog, IAgentWorkflowResolver workflowResolver)
{
    public async Task<AgentPlanResult> PlanAsync(string instruction, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return new(false, null, ["请输入自动化指令。"], "指令为空");
        var text = instruction.Trim();
        var cities = await catalog.GetCitiesAsync(cancellationToken);
        var systems = await catalog.GetSystemsAsync(cancellationToken);
        var functions = await catalog.GetFunctionsAsync(cancellationToken);
        var ambiguities = new List<string>();

        var cityMatches = cities.Where(x => Contains(text, x.Name) || Contains(text, x.Code)).ToArray();
        if (cityMatches.Length != 1) ambiguities.Add(cityMatches.Length == 0 ? "无法从指令识别城市。" : "指令匹配到多个城市，请明确城市。");
        var cityId = cityMatches.Length == 1 ? cityMatches[0].Id : Guid.Empty;

        var systemMatches = systems.Where(x => (cityId == Guid.Empty || x.CityId == cityId) && (Contains(text, x.Name) || Contains(text, x.Code))).ToArray();
        if (systemMatches.Length != 1) ambiguities.Add(systemMatches.Length == 0 ? "无法识别业务系统。" : "指令匹配到多个业务系统，请明确系统。");
        var systemId = systemMatches.Length == 1 ? systemMatches[0].Id : Guid.Empty;

        var functionMatches = functions.Where(x => (systemId == Guid.Empty || x.SystemId == systemId) && (Contains(text, x.Name) || Contains(text, x.Code))).ToArray();
        if (functionMatches.Length != 1) ambiguities.Add(functionMatches.Length == 0 ? "无法识别业务功能。" : "指令匹配到多个业务功能，请明确功能。");
        if (ambiguities.Count > 0) return new(false, null, ambiguities, "需要补充业务资源信息后才能生成执行计划。");

        var functionId = functionMatches[0].Id;
        var workflows = await workflowResolver.ResolveAsync(functionId, cancellationToken);
        if (workflows.Count == 0) return new(false, null, ["该业务功能暂无已发布的可执行 Workflow。"], "无法绑定已发布 Workflow。");
        if (workflows.Select(x => x.WorkflowId).Distinct().Count() > 1) return new(false, null, ["该业务功能存在多个已发布 Workflow，请配置默认 Workflow 后再执行。"], "Workflow 选择存在歧义。");

        var workflow = workflows[0];
        var action = ResolveAction(text);
        var risk = ResolveRisk(text);
        var parameters = ParseParameters(text);
        var plan = new TaskPlanDto(cityId, systemId, functionId, action, parameters, [], workflow.WorkflowId.ToString(), workflow.Version, risk, risk != "Low");
        return new(true, plan, [], $"已解析为：{cities.Single(x => x.Id == cityId).Name} / {systemMatches[0].Name} / {functionMatches[0].Name} / Workflow {workflow.Name} v{workflow.Version} / {action}。");
    }

    private static Dictionary<string, object?> ParseParameters(string text)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["instruction"] = text };
        var match = System.Text.RegularExpressions.Regex.Match(text, "(?:文件|Excel|表格)[:：]?[\\s]*([^，。；;\\s]+\\.(?:xlsx|xls|csv))", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success) parameters["fileName"] = match.Groups[1].Value;
        return parameters;
    }

    private static bool Contains(string text, string value) => !string.IsNullOrWhiteSpace(value) && text.Contains(value, StringComparison.OrdinalIgnoreCase);
    private static string ResolveAction(string text) => text.Contains("导出", StringComparison.OrdinalIgnoreCase) ? "Export" : text.Contains("下载", StringComparison.OrdinalIgnoreCase) ? "Download" : text.Contains("上传", StringComparison.OrdinalIgnoreCase) ? "Upload" : text.Contains("删除", StringComparison.OrdinalIgnoreCase) ? "Delete" : text.Contains("新增", StringComparison.OrdinalIgnoreCase) || text.Contains("添加", StringComparison.OrdinalIgnoreCase) ? "Create" : text.Contains("修改", StringComparison.OrdinalIgnoreCase) || text.Contains("更新", StringComparison.OrdinalIgnoreCase) ? "Update" : "Execute";
    private static string ResolveRisk(string text) => text.Contains("删除", StringComparison.OrdinalIgnoreCase) || text.Contains("注销", StringComparison.OrdinalIgnoreCase) ? "High" : text.Contains("修改", StringComparison.OrdinalIgnoreCase) || text.Contains("新增", StringComparison.OrdinalIgnoreCase) || text.Contains("提交", StringComparison.OrdinalIgnoreCase) ? "Medium" : "Low";
}
