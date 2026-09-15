using AgentRPA.Application.Agent;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Agent;

/// <summary>从已发布 Workflow 中解析业务功能对应的最新版本。</summary>
public sealed class EfAgentWorkflowResolver(AgentRpaDbContext db) : IAgentWorkflowResolver
{
    public async Task<IReadOnlyList<AgentWorkflowResource>> ResolveAsync(Guid functionId, CancellationToken cancellationToken)
    {
        return await db.Workflows
            .AsNoTracking()
            .Where(x => x.BusinessFunctionId == functionId && x.Status == AgentRPA.Domain.Workflow.WorkflowStatus.Published)
            .SelectMany(x => x.Versions
                .Where(v => v.Published)
                .Select(v => new AgentWorkflowResource(x.Id, v.Version, x.Name)))
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
    }
}
