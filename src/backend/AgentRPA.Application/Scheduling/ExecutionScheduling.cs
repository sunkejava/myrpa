namespace AgentRPA.Application.Scheduling;

/// <summary>Workflow/Task 对执行环境的硬性和偏好要求。</summary>
public sealed record ExecutionRequirement(
    IReadOnlySet<string> OsPlatforms,
    IReadOnlySet<string> NodeKinds,
    IReadOnlySet<string> Browsers,
    IReadOnlySet<string> RequiredCapabilities,
    IReadOnlySet<string> ForbiddenCapabilities,
    string? NetworkZone = null,
    Guid? NodePoolId = null,
    IReadOnlySet<Guid>? RequiredNodeIds = null,
    IReadOnlySet<Guid>? ExcludedNodeIds = null,
    IReadOnlySet<string>? RequiredHardwareIds = null,
    bool RequiresDesktopUi = false,
    string ExecutionAffinity = "Item");

/// <summary>调度候选节点的实时快照。</summary>
public sealed record ExecutionNodeSnapshot(
    Guid NodeId,
    string Name,
    string OsPlatform,
    string NodeKind,
    string Architecture,
    string Status,
    Guid? NodePoolId,
    string? NetworkZone,
    IReadOnlySet<string> Capabilities,
    IReadOnlySet<string> HardwareIds,
    int AvailableSlots,
    double LoadFactor);

/// <summary>被 Scheduler 选中的执行资源。</summary>
public sealed record ExecutionAssignment(
    Guid NodeId,
    Guid WorkerSlotId,
    Guid LeaseId,
    int Score);

/// <summary>节点注册、心跳、能力发现与调度所需的统一服务。</summary>
public interface IExecutionNodeRegistry
{
    Task<IReadOnlyList<ExecutionNodeSnapshot>> GetOnlineNodesAsync(CancellationToken cancellationToken);
}

/// <summary>一托 N 调度器。先硬过滤，再软评分，并通过租约原子占用执行资源。</summary>
public interface IExecutionScheduler
{
    Task<ExecutionAssignment?> ScheduleAsync(ExecutionRequirement requirement, Guid executionId, CancellationToken cancellationToken);
}

/// <summary>节点/Worker Slot 与硬件资源租约服务，防止同一个资源被多个任务同时占用。</summary>
public interface IExecutionLeaseService
{
    Task<ExecutionAssignment?> TryAcquireAsync(ExecutionNodeSnapshot node, Guid executionId, IReadOnlySet<string>? requiredHardwareIds, CancellationToken cancellationToken);
    Task<bool> RenewAsync(Guid leaseId, Guid executionId, CancellationToken cancellationToken);
    Task ReleaseAsync(Guid leaseId, CancellationToken cancellationToken);
}
