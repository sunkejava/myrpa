using AgentRPA.Application.Scheduling;
using AgentRPA.Domain.Execution;

namespace AgentRPA.Application.Nodes;

/// <summary>执行节点注册与心跳应用服务。</summary>
public interface INodeRegistryService : IExecutionNodeRegistry
{
    Task<ExecutionNode> RegisterAsync(NodeRegistration registration, CancellationToken cancellationToken);
    Task<bool> HeartbeatAsync(Guid nodeId, string agentVersion, DateTimeOffset heartbeatAt, CancellationToken cancellationToken);
    Task RefreshCapabilitiesAsync(Guid nodeId, IReadOnlyCollection<NodeCapabilityInput> capabilities, CancellationToken cancellationToken);
    Task RefreshWorkerSlotsAsync(Guid nodeId, IReadOnlyCollection<string> slotNames, CancellationToken cancellationToken);
    Task<int> MarkOfflineNodesAsync(TimeSpan heartbeatTimeout, CancellationToken cancellationToken);
}

/// <summary>Node Agent 注册信息。</summary>
public sealed record NodeRegistration(
    string AgentKey,
    string Name,
    NodeKind NodeKind,
    OsPlatform OsPlatform,
    string Architecture,
    string? NetworkZone,
    Guid? NodePoolId,
    string AgentVersion,
    IReadOnlyCollection<NodeCapabilityInput> Capabilities,
    IReadOnlyCollection<string> WorkerSlots);

public sealed record NodeCapabilityInput(string Code, string? Version, string? MetadataJson = null);
