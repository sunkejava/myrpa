using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.Execution;

/// <summary>执行节点类型，表示 RPA 实际运行所在的基础环境。</summary>
public enum NodeKind { Physical = 1, VirtualMachine = 2, CloudDesktop = 3, Container = 4 }
public enum OsPlatform { Windows = 1, Linux = 2, MacOS = 3, Other = 99 }
public enum NodeStatus { Offline = 0, Online = 1, Draining = 2, Disabled = 3, Unhealthy = 4, PendingApproval = 5 }

/// <summary>服务端管理的一个实际 RPA 执行环境。</summary>
public sealed class ExecutionNode : Entity
{
    private readonly List<NodeCapability> _capabilities = [];
    private ExecutionNode() { }
    public ExecutionNode(string agentKey, string name, NodeKind nodeKind, OsPlatform osPlatform, string architecture)
    { AgentKey = agentKey; Name = name; NodeKind = nodeKind; OsPlatform = osPlatform; Architecture = architecture; }
    public string AgentKey { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public NodeKind NodeKind { get; private set; }
    public OsPlatform OsPlatform { get; private set; }
    public string Architecture { get; private set; } = string.Empty;
    public NodeStatus Status { get; private set; } = NodeStatus.PendingApproval;
    public Guid? NodePoolId { get; private set; }
    public string? NetworkZone { get; private set; }
    public string? AgentVersion { get; private set; }
    public DateTimeOffset? LastHeartbeatAt { get; private set; }
    public IReadOnlyCollection<NodeCapability> Capabilities => _capabilities;
    public void RegisterHeartbeat(string agentVersion, DateTimeOffset heartbeatAt)
    {
        AgentVersion = agentVersion;
        LastHeartbeatAt = heartbeatAt;
        if (Status == NodeStatus.PendingApproval || Status == NodeStatus.Draining || Status == NodeStatus.Disabled) return;
        Status = NodeStatus.Online;
    }
    public void Approve() => Status = NodeStatus.Online;
    public void SetStatus(NodeStatus status) => Status = status;
    public void SetPool(Guid? nodePoolId) => NodePoolId = nodePoolId;
    public void SetNetworkZone(string? networkZone) => NetworkZone = networkZone;
    public void ReplaceCapabilities(IEnumerable<NodeCapability> capabilities) { _capabilities.Clear(); _capabilities.AddRange(capabilities); }
}

/// <summary>节点能力，例如 Edge、DesktopUI、UKey、Office 等。</summary>
public sealed class NodeCapability : Entity
{
    private NodeCapability() { }
    public NodeCapability(Guid nodeId, string code, string? version = null, string? metadataJson = null)
    { NodeId = nodeId; Code = code; Version = version; MetadataJson = metadataJson; }
    public Guid NodeId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string? Version { get; private set; }
    public bool Enabled { get; private set; } = true;
    public string? MetadataJson { get; private set; }
    public void Disable() => Enabled = false;
}

/// <summary>节点池，用于按地域、网络、环境等维度组织执行资源。</summary>
public sealed class NodePool : Entity
{
    private NodePool() { }
    public NodePool(string name, string? description = null) { Name = name; Description = description; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool Enabled { get; private set; } = true;
    public void Update(string name, string? description, bool enabled) { Name = name; Description = description; Enabled = enabled; }
}

/// <summary>节点上的一个并发执行槽位。ConcurrencyStamp 用于跨服务实例的乐观并发抢占。</summary>
public sealed class WorkerSlot : Entity
{
    private WorkerSlot() { }
    public WorkerSlot(Guid nodeId, string slotName) { NodeId = nodeId; SlotName = slotName; }
    public Guid NodeId { get; private set; }
    public string SlotName { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public Guid? ExecutionId { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public int ConcurrencyStamp { get; private set; }
    public bool IsAvailable(DateTimeOffset now) => Enabled && (ExecutionId is null || LeaseExpiresAt <= now);
    public void SetEnabled(bool enabled) => Enabled = enabled;
    public void Acquire(Guid executionId, DateTimeOffset expiresAt) { ExecutionId = executionId; LeaseExpiresAt = expiresAt; ConcurrencyStamp++; }
    public void Release() { ExecutionId = null; LeaseExpiresAt = null; ConcurrencyStamp++; }
}
