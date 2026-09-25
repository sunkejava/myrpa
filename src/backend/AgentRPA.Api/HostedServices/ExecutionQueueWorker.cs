using System.Text.Json;
using AgentRPA.Api.Hubs;
using AgentRPA.Api.Security;
using AgentRPA.Application.Permission;
using AgentRPA.Application.Scheduling;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using DomainTaskStatus = AgentRPA.Domain.Tasks.TaskStatus;

namespace AgentRPA.Api.HostedServices;

/// <summary>后台调度队列：将待执行 TaskItem 自动分配到满足条件的 NodeAgent/WorkerSlot。</summary>
public sealed class ExecutionQueueWorker(IServiceScopeFactory scopes, NodeAgentConnectionRegistry connections, IHubContext<NodeAgentHub, INodeAgentClient> hub, ILogger<ExecutionQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchPendingAsync(stoppingToken); await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "执行队列调度失败"); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }

    private async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgentRpaDbContext>();
        var scheduler = scope.ServiceProvider.GetRequiredService<IExecutionScheduler>();
        var permissionService = scope.ServiceProvider.GetRequiredService<PermissionService>();
        var stepPermissions = scope.ServiceProvider.GetRequiredService<WorkflowPermissionPreflight>();
        var leaseService = scope.ServiceProvider.GetRequiredService<IExecutionLeaseService>();
        var items = await db.TaskItems.Where(x => x.Status == TaskItemStatus.Pending).OrderBy(x => x.Sequence).Take(20).ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == item.TaskId, cancellationToken);
            if (task is null || task.Status == DomainTaskStatus.Draft || task.Status == DomainTaskStatus.Cancelled || task.Status == DomainTaskStatus.Succeeded) continue;
            var active = await db.Executions.AnyAsync(x => x.TaskItemId == item.Id &&
                (x.Status == ExecutionStatus.Dispatched || x.Status == ExecutionStatus.Running || x.Status == ExecutionStatus.Paused || x.Status == ExecutionStatus.WaitingForHuman), cancellationToken);
            if (active) continue;
            var version = await db.WorkflowVersions.SingleOrDefaultAsync(x => x.WorkflowId == task.WorkflowId && x.Version == task.WorkflowVersion, cancellationToken);
            if (version is null || !version.Published) continue;

            // 防止绕过 API 直接入队，审批缺失或未批准的任务永远不会派发。
            if (await TaskApprovalGate.GetStatusAsync(db, task.Id, version.DefinitionJson, cancellationToken) is { } approval && approval != TaskApprovalStatus.Approved)
            {
                task.SetStatus(approval == TaskApprovalStatus.Rejected ? DomainTaskStatus.Failed : DomainTaskStatus.Draft);
                if (approval == TaskApprovalStatus.Rejected) item.Fail("任务审批已拒绝，禁止派发。");
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            // 权限可能在任务入队后被撤销，因此真正派发到执行节点前必须再次校验。
            var businessScope = await ResolveBusinessScopeAsync(db, task.WorkflowId, cancellationToken);
            if (businessScope is null)
            {
                task.SetStatus(DomainTaskStatus.Failed);
                item.Fail("Workflow 对应业务资源不存在，任务拒绝执行。");
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }
            var permission = await permissionService.CheckAsync(task.SubjectId ?? Guid.Empty, businessScope.Value.CityId, businessScope.Value.SystemId, businessScope.Value.FunctionId, "Execute", cancellationToken);
            if (!permission.Allowed)
            {
                task.SetStatus(DomainTaskStatus.Failed);
                item.Fail("当前用户执行权限已失效，任务拒绝派发。");
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }
            var stepsAllowed = await stepPermissions.CheckAsync(task.SubjectId ?? Guid.Empty, businessScope.Value.CityId,
                businessScope.Value.SystemId, businessScope.Value.FunctionId, version.DefinitionJson, cancellationToken);
            if (!stepsAllowed.Allowed)
            {
                task.SetStatus(DomainTaskStatus.Failed);
                item.Fail($"Workflow Step 权限已失效：{stepsAllowed.Reason}");
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            // 同一 TaskItem + RetryCount 只对应一个派发键。无资源时保留 Pending Execution，下一轮继续尝试。
            var dispatchKey = $"{item.Id:N}:{item.RetryCount}";
            var execution = await db.Executions.SingleOrDefaultAsync(x => x.DispatchKey == dispatchKey, cancellationToken);
            if (execution is null)
            {
                var candidate = new Execution(item.Id, version.Id, dispatchKey);
                db.Executions.Add(candidate);
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                    execution = candidate;
                }
                catch (DbUpdateException)
                {
                    // 多实例 Worker 同时抢同一 TaskItem 时，唯一索引负责裁决；失败实例重新读取胜出的 Execution。
                    db.Entry(candidate).State = EntityState.Detached;
                    var existing = await db.Executions.SingleOrDefaultAsync(x => x.DispatchKey == dispatchKey, cancellationToken);
                    if (existing is null) throw;
                    execution = existing;
                }
            }

            // Workflow 可以进一步收紧执行节点条件；最终要求由调度器统一执行硬过滤。
            var requirement = WorkflowExecutionRequirementParser.Parse(version.DefinitionJson)
                ?? new ExecutionRequirement(new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>());
            var assignment = await scheduler.ScheduleAsync(requirement, execution.Id, cancellationToken);
            if (assignment is null)
            {
                execution.SetStatus(ExecutionStatus.Pending, "暂无满足条件的执行节点。");
                task.SetStatus(DomainTaskStatus.WaitingForResource);
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            execution.Dispatch(assignment.NodeId, assignment.WorkerSlotId);
            task.Queue();
            await db.SaveChangesAsync(cancellationToken);
            if (!connections.TryGet(assignment.NodeId, out var connectionId) || connectionId is null)
            {
                // NodeAgent 在派发瞬间掉线：恢复 TaskItem/Task 的等待状态，释放资源后由下一轮重新选择节点。
                execution.SetStatus(ExecutionStatus.Pending, "NodeAgent 未连接，等待重新调度。");
                task.SetStatus(DomainTaskStatus.WaitingForResource);
                await db.SaveChangesAsync(cancellationToken);
                await leaseService.ReleaseAsync(assignment.LeaseId, cancellationToken);
                continue;
            }
            var command = new ExecutionCommand(execution.Id, task.Id, item.Id, task.WorkflowId, task.WorkflowVersion, assignment.NodeId, assignment.WorkerSlotId, version.DefinitionJson, ParseParameters(item.InputJson));
            await hub.Clients.Client(connectionId).ExecuteAsync(command);
        }
    }

    private static async Task<(Guid CityId, Guid SystemId, Guid FunctionId)?> ResolveBusinessScopeAsync(AgentRpaDbContext db, Guid workflowId, CancellationToken ct)
    {
        var row = await db.Workflows.AsNoTracking().Where(x => x.Id == workflowId && x.Status == AgentRPA.Domain.Workflow.WorkflowStatus.Published)
            .Join(db.BusinessFunctions.AsNoTracking(), w => w.BusinessFunctionId, f => f.Id, (w, f) => new { f.Id, f.SystemId })
            .Join(db.BusinessSystems.AsNoTracking(), x => x.SystemId, s => s.Id, (x, s) => new { FunctionId = x.Id, SystemId = s.Id, s.CityId })
            .SingleOrDefaultAsync(ct);
        return row is null ? null : (row.CityId, row.SystemId, row.FunctionId);
    }

    private static IReadOnlyDictionary<string, string?> ParseParameters(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return new Dictionary<string, string?>();
            return doc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.ValueKind == JsonValueKind.Null ? null : x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { return new Dictionary<string, string?>(); }
    }
}
