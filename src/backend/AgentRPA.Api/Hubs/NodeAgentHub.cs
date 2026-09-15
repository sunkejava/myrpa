using System.Collections.Concurrent;
using AgentRPA.Application.Scheduling;
using AgentRPA.Application.Nodes;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Hubs;

/// <summary>维护 NodeAgent 与 SignalR ConnectionId 的映射。</summary>
public sealed class NodeAgentConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, string> _connections = new();
    public void Bind(Guid nodeId, string connectionId) => _connections[nodeId] = connectionId;
    public void Remove(Guid nodeId, string connectionId) => _connections.TryRemove(new KeyValuePair<Guid, string>(nodeId, connectionId));
    public bool TryGet(Guid nodeId, out string? connectionId) => _connections.TryGetValue(nodeId, out connectionId);
}

/// <summary>Server 与 NodeAgent 的实时双向通信 Hub。</summary>
public sealed class NodeAgentHub(
    NodeAgentConnectionRegistry connections,
    INodeRegistryService nodeRegistry,
    IExecutionLeaseService leases,
    AgentRpaDbContext db) : Hub<INodeAgentClient>
{
    public Task Connect(NodeAgentConnectRequest request)
    {
        connections.Bind(request.NodeId, Context.ConnectionId);
        Context.Items["NodeId"] = request.NodeId;
        return Task.CompletedTask;
    }

    public async Task<NodeHeartbeatAck> Heartbeat(NodeHeartbeatRequest request, CancellationToken cancellationToken)
    {
        if (!Context.Items.TryGetValue("NodeId", out var value) || value is not Guid nodeId || nodeId != request.NodeId)
            throw new HubException("Node 身份校验失败。");
        var accepted = await nodeRegistry.HeartbeatAsync(request.NodeId, request.AgentVersion, DateTimeOffset.UtcNow, cancellationToken);
        if (!accepted) throw new HubException("Node 不存在或已被禁用。");
        return new NodeHeartbeatAck(request.NodeId, DateTimeOffset.UtcNow, request.Status);
    }

    /// <summary>接收执行进度，同时续租执行资源；终态自动释放 Worker Slot。</summary>
    public async Task ReportProgress(ExecutionProgress progress, CancellationToken cancellationToken)
    {
        if (!Context.Items.TryGetValue("NodeId", out var value) || value is not Guid nodeId || nodeId != progress.NodeId)
            throw new HubException("Node 身份校验失败。");
        var execution = await db.Executions.SingleOrDefaultAsync(x => x.Id == progress.ExecutionId, cancellationToken);
        if (execution is null) throw new HubException("Execution 不存在。");
        if (execution.NodeId != progress.NodeId || execution.WorkerSlotId != progress.WorkerSlotId)
            throw new HubException("Execution 与 Node/WorkerSlot 不匹配。");
        if (!Enum.TryParse<ExecutionStatus>(progress.Status, true, out var status)) status = ExecutionStatus.Running;

        var lease = await db.Set<AgentRPA.Domain.Execution.NodeLease>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ExecutionId == execution.Id && !x.Released, cancellationToken);
        if (lease is not null && status is not ExecutionStatus.Succeeded and not ExecutionStatus.Failed and not ExecutionStatus.Cancelled)
        {
            if (!await leases.RenewAsync(lease.Id, execution.Id, cancellationToken))
                throw new HubException("执行租约已失效，请重新调度任务。");
        }

        execution.SetStatus(status, status == ExecutionStatus.Failed ? progress.Message : null);
        var item = await db.TaskItems.SingleOrDefaultAsync(x => x.Id == execution.TaskItemId, cancellationToken);
        if (item is not null)
        {
            if (status == ExecutionStatus.Running) item.Start();
            else if (status == ExecutionStatus.Succeeded) item.Succeed(progress.Message);
            else if (status == ExecutionStatus.Failed) item.Fail(progress.Message);
            else if (status == ExecutionStatus.Cancelled) item.Fail(progress.Message);
        }
        await db.SaveChangesAsync(cancellationToken);

        if (lease is not null && status is ExecutionStatus.Succeeded or ExecutionStatus.Failed or ExecutionStatus.Cancelled)
            await leases.ReleaseAsync(lease.Id, cancellationToken);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("NodeId", out var value) && value is Guid nodeId)
            connections.Remove(nodeId, Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
