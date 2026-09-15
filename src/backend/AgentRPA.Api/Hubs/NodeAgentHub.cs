using System.Collections.Concurrent;
using AgentRPA.Application.Scheduling;
using AgentRPA.Application.Nodes;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
namespace AgentRPA.Api.Hubs;
public sealed class NodeAgentConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, string> _connections = new();
    public void Bind(Guid nodeId, string connectionId) => _connections[nodeId] = connectionId;
    public void Remove(Guid nodeId, string connectionId) => _connections.TryRemove(new KeyValuePair<Guid, string>(nodeId, connectionId));
    public bool TryGet(Guid nodeId, out string? connectionId) => _connections.TryGetValue(nodeId, out connectionId);
}
public sealed class NodeAgentHub(NodeAgentConnectionRegistry connections, INodeRegistryService nodeRegistry, IExecutionLeaseService leases, AgentRpaDbContext db) : Hub<INodeAgentClient>
{
    public Task Connect(NodeAgentConnectRequest request) { connections.Bind(request.NodeId, Context.ConnectionId); Context.Items["NodeId"] = request.NodeId; return Task.CompletedTask; }
    public async Task<NodeHeartbeatAck> Heartbeat(NodeHeartbeatRequest request, CancellationToken cancellationToken)
    {
        if (!Context.Items.TryGetValue("NodeId", out var value) || value is not Guid nodeId || nodeId != request.NodeId) throw new HubException("Node 身份校验失败。");
        if (!await nodeRegistry.HeartbeatAsync(request.NodeId, request.AgentVersion, DateTimeOffset.UtcNow, cancellationToken)) throw new HubException("Node 不存在或已被禁用。");
        return new NodeHeartbeatAck(request.NodeId, DateTimeOffset.UtcNow, request.Status);
    }
    /// <summary>接收执行进度、续租资源，并在失败时自动进入有限重试。</summary>
    public async Task ReportProgress(ExecutionProgress progress, CancellationToken cancellationToken)
    {
        if (!Context.Items.TryGetValue("NodeId", out var value) || value is not Guid nodeId || nodeId != progress.NodeId) throw new HubException("Node 身份校验失败。");
        var execution = await db.Executions.SingleOrDefaultAsync(x => x.Id == progress.ExecutionId, cancellationToken); if (execution is null) throw new HubException("Execution 不存在。");
        if (execution.NodeId != progress.NodeId || execution.WorkerSlotId != progress.WorkerSlotId) throw new HubException("Execution 与 Node/WorkerSlot 不匹配。");
        if (!Enum.TryParse<ExecutionStatus>(progress.Status, true, out var status)) status = ExecutionStatus.Running;
        var terminal = status is ExecutionStatus.Succeeded or ExecutionStatus.Failed or ExecutionStatus.Cancelled;
        var lease = await db.Set<AgentRPA.Domain.Execution.NodeLease>().AsNoTracking().SingleOrDefaultAsync(x => x.ExecutionId == execution.Id && !x.Released, cancellationToken);
        if (lease is not null && !terminal && !await leases.RenewAsync(lease.Id, execution.Id, cancellationToken)) throw new HubException("执行租约已失效，请重新调度任务。");
        execution.SetStatus(status, status == ExecutionStatus.Failed ? progress.Message : null);
        var item = await db.TaskItems.SingleOrDefaultAsync(x => x.Id == execution.TaskItemId, cancellationToken);
        var task = item is null ? null : await db.Tasks.SingleOrDefaultAsync(x => x.Id == item.TaskId, cancellationToken);
        if (item is not null)
        {
            if (status == ExecutionStatus.Running) item.Start();
            else if (status == ExecutionStatus.Succeeded) item.Succeed(progress.Message);
            else if (status == ExecutionStatus.Failed) { item.Fail(progress.Message); if (task is not null && item.CanRetry(task.MaxRetries)) { item.Retry(); task.Queue(); } else if (task is not null) task.SetStatus(TaskStatus.Failed); }
            else if (status == ExecutionStatus.Cancelled) item.Fail(progress.Message);
        }
        if (status == ExecutionStatus.Succeeded && task is not null)
        {
            var remaining = await db.TaskItems.AnyAsync(x => x.TaskId == task.Id && x.Status != TaskItemStatus.Succeeded && x.Status != TaskItemStatus.Skipped, cancellationToken);
            if (!remaining) task.SetStatus(TaskStatus.Succeeded); else task.Queue();
        }
        await db.SaveChangesAsync(cancellationToken);
        if (lease is not null && terminal) await leases.ReleaseAsync(lease.Id, cancellationToken);
    }
    public override Task OnDisconnectedAsync(Exception? exception) { if (Context.Items.TryGetValue("NodeId", out var value) && value is Guid nodeId) connections.Remove(nodeId, Context.ConnectionId); return base.OnDisconnectedAsync(exception); }
}
