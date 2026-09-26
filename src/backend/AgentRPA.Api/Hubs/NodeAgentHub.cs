using System.Collections.Concurrent;
using System.Text.Json;
using AgentRPA.Application.Scheduling;
using AgentRPA.Application.Nodes;
using AgentRPA.Application.Workflow;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Domain.Common;
using AgentRPA.Domain.Execution;
using AgentRPA.Domain.HumanIntervention;
using AgentRPA.Domain.Resources;
using AgentRPA.Domain.Tasks;
using AgentRPA.Domain.Workflow;
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
    public async Task Connect(NodeAgentConnectRequest request)
    {
        var cancellationToken = Context.ConnectionAborted;
        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == request.NodeId, cancellationToken);
        if (node is null) throw new HubException("Node 不存在。");
        if (!string.Equals(node.AgentKey, request.AgentKey, StringComparison.Ordinal)) throw new HubException("Node 身份认证失败。");
        // 失联和异常状态由监控产生；通过身份验证的 Agent 重连可恢复，管理员停用/排空状态保持不变。
        if (node.Status is NodeStatus.Offline or NodeStatus.Unhealthy)
        {
            node.RegisterHeartbeat(request.AgentVersion, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
        if (node.Status is not NodeStatus.Online) throw new HubException($"Node 当前状态为 {node.Status}，未获准建立执行会话。");
        connections.Bind(request.NodeId, Context.ConnectionId);
        Context.Items["NodeId"] = request.NodeId;
        // A disable notification can be lost while the node is disconnected. Replay cancellation
        // on every Connect before considering any completed human intervention for resumption.
        var disabledExecutions = await (from execution in db.Executions.AsNoTracking()
            join version in db.WorkflowVersions.AsNoTracking() on execution.WorkflowVersionId equals version.Id
            join workflow in db.Workflows.AsNoTracking() on version.WorkflowId equals workflow.Id
            join item in db.TaskItems.AsNoTracking() on execution.TaskItemId equals item.Id
            join task in db.Tasks.AsNoTracking() on item.TaskId equals task.Id
            where execution.NodeId == request.NodeId && (workflow.Status == WorkflowStatus.Disabled || task.Status == DomainTaskStatus.Cancelled) &&
                (execution.Status == ExecutionStatus.Dispatched || execution.Status == ExecutionStatus.Running ||
                 execution.Status == ExecutionStatus.Paused || execution.Status == ExecutionStatus.WaitingForHuman)
            select execution.Id).ToListAsync(cancellationToken);
        foreach (var executionId in disabledExecutions) await Clients.Caller.CancelAsync(executionId);
        var resumableExecutionIds = await (from execution in db.Executions join intervention in db.HumanInterventions on execution.Id equals intervention.ExecutionId where execution.NodeId == request.NodeId && execution.Status == ExecutionStatus.WaitingForHuman && intervention.Status == InterventionStatus.Completed && !db.HumanInterventions.Any(x => x.ExecutionId == execution.Id && x.Status == InterventionStatus.Opened) select execution.Id).Distinct().ToListAsync(cancellationToken);
        foreach (var executionId in resumableExecutionIds.Except(disabledExecutions)) await Clients.Caller.ResumeAsync(executionId);
    }

    public async Task<NodeHeartbeatAck> Heartbeat(NodeHeartbeatRequest request)
    {
        var cancellationToken = Context.ConnectionAborted;
        if (!Context.Items.TryGetValue("NodeId", out var value) || value is not Guid nodeId || nodeId != request.NodeId) throw new HubException("Node 身份校验失败。");
        if (!double.IsFinite(request.CpuUsage) || request.CpuUsage is < 0 or > 100 ||
            !double.IsFinite(request.MemoryUsage) || request.MemoryUsage is < 0 or > 1_000_000 ||
            request.AvailableSlots is < 0 or > 100_000) throw new HubException("运行指标无效。");
        if (!await nodeRegistry.HeartbeatAsync(request, DateTimeOffset.UtcNow, cancellationToken)) throw new HubException("Node 不存在、已被禁用或运行指标无效。");
        return new NodeHeartbeatAck(request.NodeId, DateTimeOffset.UtcNow, request.Status);
    }

    public async Task ReportProgress(ExecutionProgress progress)
    {
        var cancellationToken = Context.ConnectionAborted;
        if (!Context.Items.TryGetValue("NodeId", out var value) || value is not Guid nodeId || nodeId != progress.NodeId) throw new HubException("Node 身份校验失败。");
        var execution = await db.Executions.SingleOrDefaultAsync(x => x.Id == progress.ExecutionId, cancellationToken); if (execution is null) throw new HubException("Execution 不存在。");
        if (execution.NodeId != progress.NodeId || execution.WorkerSlotId != progress.WorkerSlotId) throw new HubException("Execution 与 Node/WorkerSlot 不匹配。");
        if (execution.Status is ExecutionStatus.Succeeded or ExecutionStatus.Failed or ExecutionStatus.Cancelled)
            throw new HubException("已结束的 Execution 不接受新的进度报告。");
        var started = string.Equals(progress.Status, "StepStarted", StringComparison.OrdinalIgnoreCase);
        var completed = string.Equals(progress.Status, "StepCompleted", StringComparison.OrdinalIgnoreCase);
        if ((started || completed) && (string.IsNullOrWhiteSpace(progress.StepId) || progress.StepId.Length > 128 ||
            !Enum.TryParse<WorkflowStepType>(progress.StepType, true, out _)))
            throw new HubException("Step 事件缺少有效的步骤 ID 或类型。");
        var item = await db.TaskItems.SingleOrDefaultAsync(x => x.Id == execution.TaskItemId, cancellationToken);
        var task = item is null ? null : await db.Tasks.SingleOrDefaultAsync(x => x.Id == item.TaskId, cancellationToken);
        if (started && (task?.Status == DomainTaskStatus.Cancelled || await (from version in db.WorkflowVersions.AsNoTracking()
            join workflow in db.Workflows.AsNoTracking() on version.WorkflowId equals workflow.Id
            where version.Id == execution.WorkflowVersionId && workflow.Status != WorkflowStatus.Published
            select workflow.Id).AnyAsync(cancellationToken)))
        {
            await Clients.Caller.CancelAsync(execution.Id);
            throw new HubException("任务已取消或 Workflow 已停用，禁止开始后续步骤。");
        }
        if (!Enum.TryParse<ExecutionStatus>(progress.Status, true, out var status)) status = ExecutionStatus.Running;
        var safeMessage = SensitiveTextSanitizer.Sanitize(progress.Message);
        var terminal = status is ExecutionStatus.Succeeded or ExecutionStatus.Failed or ExecutionStatus.Cancelled;
        var lease = await db.Set<NodeLease>().AsNoTracking().SingleOrDefaultAsync(x => x.ExecutionId == execution.Id && !x.Released, cancellationToken);
        if (lease is not null && !terminal && !await leases.RenewAsync(lease.Id, execution.Id, cancellationToken)) throw new HubException("执行租约已失效，请重新调度任务。");
        if (task?.Status == DomainTaskStatus.Cancelled && status == ExecutionStatus.Succeeded)
        {
            // The node may have finished an external action before the cancellation reached it.
            // Require reconciliation instead of treating a late success as permission to run more items.
            status = ExecutionStatus.Cancelled;
        }
        execution.SetStatus(status == ExecutionStatus.Cancelled ? ExecutionStatus.Failed : status,
            status == ExecutionStatus.Cancelled ? "执行已取消；外部业务状态未确认，请管理员先核验。" : status == ExecutionStatus.Failed ? safeMessage : null);
        if (item is not null)
        {
            if (status == ExecutionStatus.Running) item.Start();
            else if (status == ExecutionStatus.WaitingForHuman)
            {
                task?.SetStatus(DomainTaskStatus.WaitingForHuman);
                if (task?.SubjectId is Guid ownerId && !await db.HumanInterventions.AnyAsync(x => x.ExecutionId == execution.Id && x.Status == InterventionStatus.Opened, cancellationToken))
                {
                    var intervention = new HumanIntervention(execution.Id, ownerId, InterventionType.ManualApproval,
                        $"流程步骤 {progress.StepId ?? "HumanTask"} 等待人工确认", DateTimeOffset.UtcNow.AddMinutes(30));
                    intervention.Open(null);
                    db.HumanInterventions.Add(intervention);
                }
            }
            else if (status == ExecutionStatus.Succeeded) item.Succeed(safeMessage);
            else if (status == ExecutionStatus.Failed)
            {
                item.Fail(safeMessage);
                var definition = await db.WorkflowVersions.AsNoTracking().Where(x => x.Id == execution.WorkflowVersionId)
                    .Select(x => x.DefinitionJson).SingleOrDefaultAsync(cancellationToken);
                if (task is not null && task.Status != DomainTaskStatus.Cancelled && item.CanRetry(task.MaxRetries) && definition is not null && WorkflowRetrySafety.IsSafeToRetry(definition))
                {
                    item.Retry(); task.Queue();
                }
                else if (task is not null) task.SetStatus(DomainTaskStatus.Failed);
            }
            else if (status == ExecutionStatus.Cancelled)
            {
                item.Fail("执行已取消；外部业务状态未确认，请管理员先核验。");
                task?.SetStatus(DomainTaskStatus.Failed);
            }
        }
        if (status == ExecutionStatus.Succeeded && task is not null)
        {
            // The current item's Succeeded state is still only tracked in memory until SaveChanges.
            var remaining = await db.TaskItems.AnyAsync(x => x.TaskId == task.Id && x.Id != execution.TaskItemId &&
                x.Status != TaskItemStatus.Succeeded && x.Status != TaskItemStatus.Skipped, cancellationToken);
            if (!remaining) task.SetStatus(DomainTaskStatus.Succeeded); else task.Queue();
        }
        var nextSequence = (await db.ExecutionLogs.Where(x => x.ExecutionId == execution.Id).Select(x => (long?)x.Sequence).MaxAsync(cancellationToken) ?? -1) + 1;
        var level = status == ExecutionStatus.Failed ? ExecutionLogLevel.Error : ExecutionLogLevel.Information;
        var eventType = started ? ExecutionLogEventType.StepStarted : completed ? ExecutionLogEventType.StepCompleted : ExecutionLogEventType.Execution;
        db.ExecutionLogs.Add(new ExecutionLog(execution.Id, nextSequence, level, eventType,
            started || completed ? safeMessage ?? $"Step {progress.StepId}" : $"NodeAgent 报告执行状态：{status}",
            progress.StepId, JsonSerializer.Serialize(new { progressPercent = progress.ProgressPercent, stepType = progress.StepType })));
        await db.SaveChangesAsync(cancellationToken);
        if (lease is not null && terminal) await leases.ReleaseAsync(lease.Id, cancellationToken);
    }

    public async Task ReportArtifact(ExecutionArtifactReport report)
    {
        var cancellationToken = Context.ConnectionAborted;
        if (!Context.Items.TryGetValue("NodeId", out var value) || value is not Guid nodeId || nodeId != report.NodeId) throw new HubException("Node 身份校验失败。");
        if (report.ExecutionId == Guid.Empty || report.WorkerSlotId == Guid.Empty || string.IsNullOrWhiteSpace(report.FileName) || report.FileName.Length > 260 || string.IsNullOrWhiteSpace(report.StorageKey) || report.StorageKey.Length > 1024 || report.Size < 0 || report.Size > 10L * 1024 * 1024 * 1024) throw new HubException("产物元数据无效。");
        var execution = await db.Executions.SingleOrDefaultAsync(x => x.Id == report.ExecutionId, cancellationToken);
        if (execution is null || execution.NodeId != report.NodeId || execution.WorkerSlotId != report.WorkerSlotId) throw new HubException("Execution 与 Node/WorkerSlot 不匹配。");
        if (string.IsNullOrWhiteSpace(report.ArtifactType) || report.ArtifactType.Length > 64) throw new HubException("产物类型无效。");
        var duplicate = await db.ExecutionArtifacts.AnyAsync(x => x.ExecutionId == execution.Id && x.StorageKey == report.StorageKey && x.Sha256 == report.Hash, cancellationToken);
        if (duplicate) return;
        var expiresAt = report.ExpiresAt;
        if (expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow) throw new HubException("产物过期时间必须晚于当前时间。");
        db.ExecutionArtifacts.Add(new ExecutionArtifact(execution.Id, execution.TaskItemId, report.ArtifactType, report.FileName, report.StorageKey, report.ContentType, report.Size, report.Hash, expiresAt));
        var nextSequence = (await db.ExecutionLogs.Where(x => x.ExecutionId == execution.Id).Select(x => (long?)x.Sequence).MaxAsync(cancellationToken) ?? -1) + 1;
        db.ExecutionLogs.Add(new ExecutionLog(execution.Id, nextSequence, ExecutionLogLevel.Information, ExecutionLogEventType.Artifact, $"NodeAgent 登记运行时产物：{report.ArtifactType}", null, JsonSerializer.Serialize(new { report.FileName, report.ContentType, report.Size, report.Hash })));
        await db.SaveChangesAsync(cancellationToken);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("NodeId", out var value) && value is Guid nodeId) connections.Remove(nodeId, Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
