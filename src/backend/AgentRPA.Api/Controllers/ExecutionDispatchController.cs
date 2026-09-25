using AgentRPA.Api.Hubs;
using AgentRPA.Api.Security;
using AgentRPA.Application.Permission;
using AgentRPA.Application.Scheduling;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Domain.Execution;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

[ApiController, Route("api/executions"), Authorize]
public sealed class ExecutionDispatchController(
    AgentRpaDbContext db,
    PermissionService permissionService,
    IExecutionScheduler scheduler,
    IExecutionLeaseService leaseService,
    NodeAgentConnectionRegistry connections,
    IHubContext<NodeAgentHub, INodeAgentClient> hub) : ControllerBase
{
    [HttpPost("dispatch")]
    public async Task<IActionResult> Dispatch(DispatchRequest r, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId))
            return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });

        var item = await db.TaskItems.SingleOrDefaultAsync(x => x.Id == r.TaskItemId, ct);
        if (item is null)
            return NotFound();
        if (item.Status != TaskItemStatus.Pending)
            return Conflict(new { message = "只有 Pending 状态的任务项允许手工派发。" });

        var task = await db.Tasks.SingleAsync(x => x.Id == item.TaskId, ct);
        if (task.SubjectId != subjectId)
            return Forbid();
        if (task.Status is AgentRPA.Domain.Tasks.TaskStatus.Draft
            or AgentRPA.Domain.Tasks.TaskStatus.Cancelled
            or AgentRPA.Domain.Tasks.TaskStatus.Succeeded)
            return Conflict(new { message = "当前任务状态不允许执行。" });

        var scope = await ResolveBusinessScopeAsync(task.WorkflowId, ct);
        if (scope is null)
            return BadRequest(new { message = "Workflow 对应业务资源不存在。" });

        // 手工派发同样必须经过城市/系统/功能/动作四级权限检查。
        var permission = await permissionService.CheckAsync(
            subjectId,
            scope.Value.CityId,
            scope.Value.SystemId,
            scope.Value.FunctionId,
            "Execute",
            ct);
        if (!permission.Allowed)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = permission.Reason });

        var v = await db.WorkflowVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.WorkflowId == task.WorkflowId && x.Version == task.WorkflowVersion && x.Published, ct);
        if (v is null)
            return Conflict(new { message = "任务引用的工作流版本未发布。" });

        // 手工指定节点只是“候选范围收窄”，仍由统一 Scheduler 执行 OS、能力、NodePool、硬件与 WorkerSlot 租约校验。
        var workflowRequirement = WorkflowExecutionRequirementParser.Parse(v.DefinitionJson)
            ?? new ExecutionRequirement(new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>());
        // 手动派发只能收窄 Workflow 允许的节点范围，不能通过并集加入不允许的节点。
        if (workflowRequirement.RequiredNodeIds is { Count: > 0 } allowedNodes && !allowedNodes.Contains(r.NodeId))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "指定节点不在 Workflow 允许的节点范围内。" });
        var requiredNodes = new HashSet<Guid> { r.NodeId };
        var requirement = workflowRequirement with { RequiredNodeIds = requiredNodes };

        var dispatchKey = $"{item.Id:N}:{item.RetryCount}";
        var existing = await db.Executions.SingleOrDefaultAsync(x => x.DispatchKey == dispatchKey, ct);
        if (existing is not null)
            return Accepted(new { existing.Id, existing.Status });

        var e = new Execution(item.Id, v.Id, dispatchKey);
        db.Executions.Add(e);
        await db.SaveChangesAsync(ct);

        var assignment = await scheduler.ScheduleAsync(requirement, e.Id, ct);
        if (assignment is null)
        {
            e.SetStatus(ExecutionStatus.Pending, "指定执行节点当前不满足 Workflow 执行要求或资源不可用。");
            task.SetStatus(AgentRPA.Domain.Tasks.TaskStatus.WaitingForResource);
            await db.SaveChangesAsync(ct);
            return BadRequest(new { message = "指定执行节点当前不可用，或不满足 Workflow 的执行资源约束。" });
        }

        e.Dispatch(assignment.NodeId, assignment.WorkerSlotId);
        task.Queue();
        await db.SaveChangesAsync(ct);

        if (!connections.TryGet(assignment.NodeId, out var cid) || cid is null)
        {
            // 派发瞬间 NodeAgent 掉线：Execution 回到 Pending，租约释放，由后台队列重新调度。
            e.SetStatus(ExecutionStatus.Pending, "NodeAgent 未连接，等待重新调度。");
            task.SetStatus(AgentRPA.Domain.Tasks.TaskStatus.WaitingForResource);
            await db.SaveChangesAsync(ct);
            await leaseService.ReleaseAsync(assignment.LeaseId, ct);
            return Accepted(new { e.Id, e.Status, message = "NodeAgent 暂时离线，已回到等待资源状态。" });
        }

        await hub.Clients.Client(cid).ExecuteAsync(new ExecutionCommand(
            e.Id,
            task.Id,
            item.Id,
            task.WorkflowId,
            task.WorkflowVersion,
            assignment.NodeId,
            assignment.WorkerSlotId,
            v.DefinitionJson,
            ParseParameters(item.InputJson)));

        return Accepted(new { e.Id, e.Status });
    }

    private static IReadOnlyDictionary<string, string?> ParseParameters(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return new Dictionary<string, string?>();
            return doc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.ValueKind == System.Text.Json.JsonValueKind.Null ? null : x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        }
        catch (System.Text.Json.JsonException) { return new Dictionary<string, string?>(); }
    }

    private async Task<(Guid CityId, Guid SystemId, Guid FunctionId)?> ResolveBusinessScopeAsync(Guid workflowId, CancellationToken ct)
    {
        var row = await db.Workflows.AsNoTracking().Where(x => x.Id == workflowId)
            .Join(db.BusinessFunctions.AsNoTracking(), w => w.BusinessFunctionId, f => f.Id, (w, f) => new { f.Id, f.SystemId })
            .Join(db.BusinessSystems.AsNoTracking(), x => x.SystemId, s => s.Id, (x, s) => new { FunctionId = x.Id, SystemId = s.Id, s.CityId })
            .SingleOrDefaultAsync(ct);
        return row is null ? null : (row.CityId, row.SystemId, row.FunctionId);
    }
}

public sealed record DispatchRequest(Guid TaskItemId, Guid NodeId, Guid WorkerSlotId);
