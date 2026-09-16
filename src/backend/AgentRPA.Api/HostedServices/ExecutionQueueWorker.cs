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
        var leaseService = scope.ServiceProvider.GetRequiredService<IExecutionLeaseService>();
        var items = await db.TaskItems.Where(x => x.Status == TaskItemStatus.Pending).OrderBy(x => x.Sequence).Take(20).ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == item.TaskId, cancellationToken);
            if (task is null || task.Status == DomainTaskStatus.Draft || task.Status == DomainTaskStatus.Cancelled || task.Status == DomainTaskStatus.Succeeded) continue;
            var active = await db.Executions.AnyAsync(x => x.TaskItemId == item.Id &&
                (x.Status == ExecutionStatus.Pending || x.Status == ExecutionStatus.Dispatched || x.Status == ExecutionStatus.Running || x.Status == ExecutionStatus.Paused || x.Status == ExecutionStatus.WaitingForHuman), cancellationToken);
            if (active) continue;
            var version = await db.WorkflowVersions.SingleOrDefaultAsync(x => x.WorkflowId == task.WorkflowId && x.Version == task.WorkflowVersion, cancellationToken);
            if (version is null || !version.Published) continue;

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

            var execution = new Execution(item.Id, version.Id);
            db.Executions.Add(execution);
            await db.SaveChangesAsync(cancellationToken);

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
                execution.SetStatus(ExecutionStatus.Failed, "NodeAgent 未连接。");
                item.Fail("NodeAgent 未连接。");
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
        var row = await db.Workflows.AsNoTracking().Where(x => x.Id == workflowId)
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
