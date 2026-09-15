namespace AgentRPA.Contracts.Nodes;

/// <summary>执行节点首次注册请求。</summary>
public sealed record RegisterNodeRequest(
    string AgentKey,
    string Name,
    string NodeKind,
    string OsPlatform,
    string Architecture,
    string AgentVersion,
    string? NetworkZone,
    Guid? NodePoolId,
    IReadOnlyList<NodeCapabilityDto> Capabilities,
    IReadOnlyList<string> WorkerSlots);

public sealed record NodeCapabilityDto(string Code, string? Version = null, string? MetadataJson = null);

/// <summary>节点心跳请求。</summary>
public sealed record NodeHeartbeatRequest(
    Guid NodeId,
    string AgentVersion,
    string Status,
    double CpuUsage,
    double MemoryUsage,
    int AvailableSlots,
    DateTimeOffset SentAt);

/// <summary>服务端派发给 Node Agent 的执行命令。</summary>
public sealed record ExecutionCommand(
    Guid ExecutionId,
    Guid TaskId,
    Guid TaskItemId,
    Guid WorkflowId,
    int WorkflowVersion,
    Guid NodeId,
    Guid WorkerSlotId,
    string WorkflowPayload,
    IReadOnlyDictionary<string, string?> Parameters);

/// <summary>节点向服务端报告执行进度。</summary>
public sealed record ExecutionProgress(
    Guid ExecutionId,
    Guid NodeId,
    Guid WorkerSlotId,
    string Status,
    string? StepId,
    int? ProgressPercent,
    string? Message,
    DateTimeOffset OccurredAt);
