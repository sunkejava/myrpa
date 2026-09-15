using AgentRPA.Contracts.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Application.Agent;

public sealed record AgentPlanResult(bool Success, TaskPlanDto? Plan, IReadOnlyList<string> Ambiguities, string Summary);

/// <summary>Agent 第一阶段规划器：先用本地资源目录做确定性解析，后续可由 LLM Provider 实现同一契约。</summary>
public sealed class AgentPlanningService(AgentRpaDbContext db)
{
    public async Task<AgentPlanResult> PlanAsync(string instruction, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instruction)) return new(false, null, ["请输入自动化指令。"], "指令为空");
        var text = instruction.Trim();
        var cities = await db.Cities.AsNoTracking().Select(x => new { x.Id, x.Name, x.Code }).ToListAsync(cancellationToken);
        var systems = await db.BusinessSystems.AsNoTracking().Select(x => new { x.Id, x.CityId, x.Name, x.Code }).ToListAsync(cancellationToken);
        var functions = await db.BusinessFunctions.AsNoTracking().Select(x => new { x.Id, x.BusinessSystemId, x.Name, x.Code }).ToListAsync(cancellationToken);
        var ambiguities = new List<string>();

        var cityMatches = cities.Where(x => Contains(text, x.Name) || Contains(text, x.Code)).ToArray();
        if (cityMatches.Length != 1) ambiguities.Add(cityMatches.Length == 0 ? "无法从指令识别城市。" : "指令匹配到多个城市，请明确城市。");

        var cityId = cityMatches.Length == 1 ? cityMatches[0].Id : Guid.Empty;
        var systemMatches = systems.Where(x => (cityId == Guid.Empty || x.CityId == cityId) && (Contains(text, x.Name) || Contains(text, x.Code))).ToArray();
        if (systemMatches.Length != 1) ambiguities.Add(systemMatches.Length == 0 ? "无法识别业务系统。" : "指令匹配到多个业务系统，请明确系统。 ");

        var systemId = systemMatches.Length == 1 ? systemMatches[0].Id : Guid.Empty;
        var functionMatches = functions.Where(x => (systemId == Guid.Empty || x.BusinessSystemId == systemId) && (Contains(text, x.Name) || Contains(text, x.Code))).ToArray();
        if (functionMatches.Length != 1) ambiguities.Add(functionMatches.Length == 0 ? "无法识别业务功能。" : "指令匹配到多个业务功能，请明确功能。 ");

        if (ambiguities.Count > 0) return new(false, null, ambiguities, "需要补充业务资源信息后才能生成执行计划。");

        var action = ResolveAction(text);
        var risk = ResolveRisk(text);
        var plan = new TaskPlanDto(cityId, systemId, functionMatches[0].Id, action,
            new Dictionary<string, object?> { ["instruction"] = text }, [], null, null, risk, risk != "Low");
        return new(true, plan, [], $"已解析为：{cities.Single(x => x.Id == cityId).Name} / {systemMatches[0].Name} / {functionMatches[0].Name} / {action}。");
    }

    private static bool Contains(string text, string value) => !string.IsNullOrWhiteSpace(value) && text.Contains(value, StringComparison.OrdinalIgnoreCase);
    private static string ResolveAction(string text)
    {
        if (text.Contains("导出", StringComparison.OrdinalIgnoreCase)) return "Export";
        if (text.Contains("下载", StringComparison.OrdinalIgnoreCase)) return "Download";
        if (text.Contains("上传", StringComparison.OrdinalIgnoreCase)) return "Upload";
        if (text.Contains("删除", StringComparison.OrdinalIgnoreCase)) return "Delete";
        if (text.Contains("新增", StringComparison.OrdinalIgnoreCase) || text.Contains("添加", StringComparison.OrdinalIgnoreCase)) return "Create";
        if (text.Contains("修改", StringComparison.OrdinalIgnoreCase) || text.Contains("更新", StringComparison.OrdinalIgnoreCase)) return "Update";
        return "Execute";
    }

    private static string ResolveRisk(string text)
    {
        if (text.Contains("删除", StringComparison.OrdinalIgnoreCase) || text.Contains("注销", StringComparison.OrdinalIgnoreCase)) return "High";
        if (text.Contains("修改", StringComparison.OrdinalIgnoreCase) || text.Contains("新增", StringComparison.OrdinalIgnoreCase) || text.Contains("提交", StringComparison.OrdinalIgnoreCase)) return "Medium";
        return "Low";
    }
}
