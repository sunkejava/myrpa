using System.Collections.Concurrent;
using AgentRPA.Application.Scheduling;
using AgentRPA.Application.Nodes;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Domain.Execution;
using AgentRPA.Domain.HumanIntervention;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using DomainTaskStatus = AgentRPA.Domain.Tasks.TaskStatus;

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
    /// <summary>建立 SignalR 会话前再次校验 NodeId + AgentKey，防止伪造节点连接。</summary>
    public async Task Connect(NodeAgentConnectRequest request, CancellationToken cancellationToken)
    {
        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == request.NodeId, cancellationToken);
        if (node is null) throw new HubException("Node 不存在。");
        if (!string.Equals(node.AgentKey, request.AgentKey, StringComparison.Ordinal)) throw new HubException("Node 身份认证失败。");
        if (node.Status is not NodeStatus.Online)
            throw new HubException($"Node 当前状态为 {node.Status}，未获准建立执行会话。");

        connections.Bind(request.NodeId, Context.ConnectionId);
        Context.Items["NodeId"] = request.NodeId;

        // 如果 SignalR 短暂断线期间用户已经完成扫码，原 NodeAgent 进程中的浏览器 Session 仍可能存活。
        // 连接恢复后补发 Resume，避免人工完成后任务永久停留在 WaitingForHuman。
        var resumableExecutionIds = await (from execution in db.Executions
                                           join intervention in db.HumanInterventions on execution.Id equals intervention.ExecutionId
                                           where execution.NodeId == request.NodeId &&
                                                 execution.Status == ExecutionStatus.WaitingForHuman &&
                                                 intervention.Status == InterventionStatus.Completed
                                           select execution.Id)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var executionId in resumableExecutionIds)
            await Clients.Caller.ResumeAsync(executionId);
    }

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
        var terminal = status == ExecutionStatus.Succeeded || status == ExecutionStatus.Failed || status == ExecutionStatus.Cancelled;
        var lease = await db.Set<NodeLease>().AsNoTracking().SingleOrDefaultAsync(x => x.ExecutionId == execution.Id && !x.Released, cancellationToken);
        if (lease is not null && !terminal && !await leases.RenewAsync(lease.Id, execution.Id, cancellationToken)) throw new HubException("执行租约已失效，请重新调度任务。");

        execution.SetStatus(status, status == ExecutionStatus.Failed ? progress.Message : null);
        var item = await db.TaskItems.SingleOrDefaultAsync(x => x.Id == execution.TaskItemId, cancellationToken);
        var task = item is null ? null : await db.Tasks.SingleOrDefaultAsync(x => x.Id == item.TaskId, cancellationToken);
        if (item is not null)
        {
            if (status == ExecutionStatus.Running) item.Start();
            else if (status == ExecutionStatus.WaitingForHuman)
            {
                task?.SetStatus(DomainTaskStatus.WaitingForHuman);
            }
            else if (status == ExecutionStatus.Succeeded) item.Succeed(progress.Message);
            else if (status == ExecutionStatus.Failed)
            {
                item.Fail(progress.Message);
                if (task is not null && item.CanRetry(task.MaxRetries)) { item.Retry(); task.Queue(); }
                else if (task is not null) task.SetStatus(DomainTaskStatus.Failed);
            }
            else if (status == ExecutionStatus.Cancelled) item.Fail(progress.Message);
        }
        if (status == ExecutionStatus.Succeeded && task is not null)
        {
            var remaining = await db.TaskItems.AnyAsync(x => x.TaskId == task.Id && x.Status != TaskItemStatus.Succeeded && x.Status != TaskItemStatus.Skipped, cancellationToken);
            if (!remaining) task.SetStatus(DomainTaskStatus.Succeeded); else task.Queue();
        }

        // 不持久化 NodeAgent 返回的原始 Message，避免密码、Token、Cookie、PIN 等敏感内容进入 ExecutionLog。
        var nextSequence = (await db.ExecutionLogs
            .Where(x => x.ExecutionId == execution.Id)
            .Select(x => (long?)x.Sequence)
            .MaxAsync(cancellationToken) ?? -1) + 1;
        var level = status == ExecutionStatus.Failed ? ExecutionLogLevel.Error : ExecutionLogLevel.Information;
        db.ExecutionLogs.Add(new ExecutionLog(
            execution.Id,
            nextSequence,
            level,
            ExecutionLogEventType.Execution,
            $"NodeAgent 报告执行状态：{status}",
            progress.StepId,
            $"{{\"progressPercent\":{(progress.ProgressPercent.HasValue ? progress.ProgressPercent.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null")}}}"));

        await db.SaveChangesAsync(cancellationToken);
        if (lease is not null && terminal) await leases.ReleaseAsync(lease.Id, cancellationToken);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("NodeId", out var value) && value is Guid nodeId) connections.Remove(nodeId, Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
