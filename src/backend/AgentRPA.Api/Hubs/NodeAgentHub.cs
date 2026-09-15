using System.Collections.Concurrent;
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
    AgentRpaDbContext db) : Hub<INodeAgentClient>
{
    public Task Connect(NodeAgentConnectRequest request)
    {
        connections.Bind(request.NodeId, Context.ConnectionId);
        Context.Items["NodeId"] = request.NodeId;
        return Task.CompletedTask;
    }

    /// <summary>NodeAgent 通过 SignalR 上报心跳，服务端返回统一 ACK。</summary>
    public async Task<NodeHeartbeatAck> Heartbeat(NodeHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var accepted = await nodeRegistry.HeartbeatAsync(
            request.NodeId,
            request.AgentVersion,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!accepted)
            throw new HubException("Node 不存在或已被禁用。");

        connections.Bind(request.NodeId, Context.ConnectionId);
        Context.Items["NodeId"] = request.NodeId;
        return new NodeHeartbeatAck(request.NodeId, DateTimeOffset.UtcNow, request.Status);
    }

    /// <summary>接收执行进度并同步 Execution 与 TaskItem 状态。</summary>
    public async Task ReportProgress(ExecutionProgress progress, CancellationToken cancellationToken)
    {
        if (!Context.Items.TryGetValue("NodeId", out var value) || value is not Guid nodeId || nodeId != progress.NodeId)
            throw new HubException("Node 身份校验失败。");

        var execution = await db.Executions.SingleOrDefaultAsync(x => x.Id == progress.ExecutionId, cancellationToken);
        if (execution is null)
            throw new HubException("Execution 不存在。");

        if (execution.NodeId != progress.NodeId || execution.WorkerSlotId != progress.WorkerSlotId)
            throw new HubException("Execution 与 Node/WorkerSlot 不匹配。");

        if (!Enum.TryParse<ExecutionStatus>(progress.Status, true, out var status))
            status = ExecutionStatus.Running;

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
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("NodeId", out var value) && value is Guid nodeId)
            connections.Remove(nodeId, Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
