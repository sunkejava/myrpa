using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.Execution;

/// <summary>执行节点/Worker Slot 的持久化租约。</summary>
public sealed class NodeLease : Entity
{
    private NodeLease() { }

    public NodeLease(Guid executionId, Guid nodeId, Guid workerSlotId, DateTimeOffset expiresAt)
    {
        ExecutionId = executionId;
        NodeId = nodeId;
        WorkerSlotId = workerSlotId;
        ExpiresAt = expiresAt;
        LastHeartbeatAt = DateTimeOffset.UtcNow;
    }

    public Guid ExecutionId { get; private set; }
    public Guid NodeId { get; private set; }
    public Guid WorkerSlotId { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset LastHeartbeatAt { get; private set; }
    public bool Released { get; private set; }

    public bool IsActive(DateTimeOffset now) => !Released && ExpiresAt > now;

    public void Renew(DateTimeOffset expiresAt, DateTimeOffset heartbeatAt)
    {
        if (Released) throw new InvalidOperationException("Cannot renew a released lease.");
        ExpiresAt = expiresAt;
        LastHeartbeatAt = heartbeatAt;
    }

    public void Release() => Released = true;
}

/// <summary>独占资源锁，例如 UKey、智能卡、桌面会话等。</summary>
public sealed class ResourceLock : Entity
{
    private ResourceLock() { }

    public ResourceLock(string resourceType, string resourceId, Guid executionId, DateTimeOffset expiresAt)
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
        ExecutionId = executionId;
        ExpiresAt = expiresAt;
        LastHeartbeatAt = DateTimeOffset.UtcNow;
    }

    public string ResourceType { get; private set; } = string.Empty;
    public string ResourceId { get; private set; } = string.Empty;
    public Guid ExecutionId { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset LastHeartbeatAt { get; private set; }
    public bool Released { get; private set; }

    public bool IsActive(DateTimeOffset now) => !Released && ExpiresAt > now;

    public void Renew(DateTimeOffset expiresAt, DateTimeOffset heartbeatAt)
    {
        if (Released) throw new InvalidOperationException("Cannot renew a released resource lock.");
        ExpiresAt = expiresAt;
        LastHeartbeatAt = heartbeatAt;
    }

    public void Release() => Released = true;
}
