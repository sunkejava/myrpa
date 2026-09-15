using AgentRPA.Application.Agent;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Agent;

/// <summary>从已发布 Workflow 中解析业务功能对应的版本，并识别 Definition 中声明的默认版本。</summary>
public sealed class EfAgentWorkflowResolver(AgentRpaDbContext db) : IAgentWorkflowResolver
{
    public async Task<IReadOnlyList<AgentWorkflowResource>> ResolveAsync(Guid functionId, CancellationToken cancellationToken)
    {
        var workflows = await db.Workflows
            .AsNoTracking()
            .Where(x => x.BusinessFunctionId == functionId && x.Status == AgentRPA.Domain.Workflow.WorkflowStatus.Published)
            .SelectMany(x => x.Versions
                .Where(v => v.Published)
                .Select(v => new { x.Id, x.Name, v.Version, v.DefinitionJson }))
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
