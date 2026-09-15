namespace AgentRPA.Application.Agent;

/// <summary>Agent 工作流解析结果，保证 Agent 只选择已发布且可执行的版本。</summary>
public sealed record AgentWorkflowResource(Guid WorkflowId, int Version, string Name);

/// <summary>根据业务功能解析可执行 Workflow 的应用层抽象。</summary>
public interface IAgentWorkflowResolver
{
    Task<IReadOnlyList<AgentWorkflowResource>> ResolveAsync(Guid functionId, CancellationToken cancellationToken);
}
