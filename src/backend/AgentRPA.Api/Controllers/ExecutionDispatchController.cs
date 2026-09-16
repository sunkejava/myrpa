using AgentRPA.Api.Hubs;
using AgentRPA.Api.Security;
using AgentRPA.Application.Permission;
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
    NodeAgentConnectionRegistry connections,
    IHubContext<NodeAgentHub, INodeAgentClient> hub) : ControllerBase
{
    [HttpPost("dispatch")]
    public async Task<IActionResult> Dispatch(DispatchRequest r, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId))
            return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });

        var item = await db.TaskItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.TaskItemId, ct);
        if (item is null)
            return NotFound();
        if (item.Status != TaskItemStatus.Pending)
            return Conflict(new { message = "只有 Pending 状态的任务项允许手工派发。" });

        var task = await db.Tasks.AsNoTracking().SingleAsync(x => x.Id == item.TaskId, ct);
        if (task.SubjectId != subjectId)
            return Forbid();
        if (task.Status is TaskStatus.Draft or TaskStatus.Cancelled or TaskStatus.Succeeded)
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

        var node = await db.ExecutionNodes.SingleOrDefaultAsync(
            x => x.Id == r.NodeId && x.Status == NodeStatus.Online, ct);
        var slot = node is null
            ? null
            : await db.WorkerSlots.SingleOrDefaultAsync(x => x.Id == r.WorkerSlotId && x.NodeId == node.Id, ct);

        if (node is null || slot is null || !slot.IsAvailable(DateTimeOffset.UtcNow))
            return BadRequest(new { message = "执行资源不可用。" });

        // 手工派发也必须遵循 TaskItem + RetryCount 的幂等约束，避免重复创建 Execution。
        var dispatchKey = $"{item.Id:N}:{item.RetryCount}";
        var existing = await db.Executions.SingleOrDefaultAsync(x => x.DispatchKey == dispatchKey, ct);
        if (existing is not null)
            return Accepted(new { existing.Id, existing.Status });

        var e = new Execution(item.Id, v.Id, dispatchKey);
        e.Dispatch(node.Id, slot.Id);
        slot.Acquire(e.Id, DateTimeOffset.UtcNow.AddMinutes(15));
        db.Executions.Add(e);
        await db.SaveChangesAsync(ct);

        if (!connections.TryGet(node.Id, out var cid) || cid is null)
        {
            e.SetStatus(ExecutionStatus.Failed, "NodeAgent 未连接。");
            slot.Release();
            await db.SaveChangesAsync(ct);
            return Conflict(new { message = "NodeAgent 未连接。" });
        }

        await hub.Clients.Client(cid).ExecuteAsync(new ExecutionCommand(
            e.Id,
            task.Id,
            item.Id,
            task.WorkflowId,
            task.WorkflowVersion,
            node.Id,
            slot.Id,
            v.DefinitionJson,
            new Dictionary<string, string?>()));

        return Accepted(new { e.Id, e.Status });
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
