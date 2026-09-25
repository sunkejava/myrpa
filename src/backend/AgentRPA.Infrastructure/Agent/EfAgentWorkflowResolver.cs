using AgentRPA.Application.Agent;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Agent;

/// <summary>从已发布 Workflow 中解析业务功能对应的版本，并识别 Definition 中声明的默认版本。</summary>
public sealed class EfAgentWorkflowResolver(AgentRpaDbContext db) : IAgentWorkflowResolver
{
    public async Task<IReadOnlyList<AgentWorkflowResource>> ResolveAsync(Guid functionId, CancellationToken cancellationToken)
    {
        // SQLite 不支持某些相关 SelectMany 翻译出的 APPLY；使用明确的等值 JOIN。
        var workflows = await (from workflow in db.Workflows.AsNoTracking()
            join version in db.WorkflowVersions.AsNoTracking() on workflow.Id equals version.WorkflowId
            where workflow.BusinessFunctionId == functionId &&
                  workflow.Status == AgentRPA.Domain.Workflow.WorkflowStatus.Published && version.Published
            select new { workflow.Id, workflow.Name, version.Version, version.DefinitionJson })
            .ToListAsync(cancellationToken);

        return workflows
            .Select(x => new AgentWorkflowResource(x.Id, x.Version, x.Name, IsDefault(x.DefinitionJson)))
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.Version)
            .ToList();
    }

    private static bool IsDefault(string definitionJson)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(definitionJson);
            return document.RootElement.TryGetProperty("isDefault", out var value) &&
                   value.ValueKind == System.Text.Json.JsonValueKind.True;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
