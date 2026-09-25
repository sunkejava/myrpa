using AgentRPA.Api.Security;
using AgentRPA.Application.Agent;
using AgentRPA.Application.Permission;
using AgentRPA.Application.Workflow;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>自然语言 Agent 控制器：规划、权限预检查以及经确认后的 Task 创建。</summary>
[ApiController, Route("api/agent"), Authorize]
public sealed class AgentController(AgentPlanningService planner, PermissionService permissionService, WorkflowPermissionPreflight stepPermissions, AgentRpaDbContext db) : ControllerBase
{
    [HttpPost("plan")]
    public async Task<IActionResult> Plan(AgentPlanRequest request, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体（sub/nameidentifier）。" });
        var result = await planner.PlanAsync(request.Instruction, subjectId, ct);
        return result.Success ? Ok(result) : UnprocessableEntity(result);
    }

    [HttpPost("check-permission")]
    public async Task<IActionResult> CheckPermission(AgentPermissionCheckRequest request, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体（sub/nameidentifier）。" });
        var result = await permissionService.CheckAsync(subjectId, request.CityId, request.SystemId, request.FunctionId, request.Action, ct);
        return result.Allowed ? Ok(result) : StatusCode(StatusCodes.Status403Forbidden, result);
    }

    /// <summary>自然语言 → Plan → Permission → Confirmation → Task。Agent 不直接操作浏览器。</summary>
    [HttpPost("execute")]
    public async Task<IActionResult> Execute(AgentExecuteRequest request, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体（sub/nameidentifier）。" });
        var planResult = await planner.PlanAsync(request.Instruction, subjectId, ct);
        if (!planResult.Success || planResult.Plan is null) return UnprocessableEntity(planResult);
        var plan = planResult.Plan;
        var permission = await permissionService.CheckAsync(subjectId, plan.CityId, plan.SystemId, plan.FunctionId, plan.Action, ct);
        if (!permission.Allowed) return StatusCode(StatusCodes.Status403Forbidden, permission);
        if (plan.RequiresConfirmation && !request.Confirmed)
            return StatusCode(StatusCodes.Status428PreconditionRequired, new { confirmationRequired = true, message = "该操作需要用户明确确认后才能创建执行任务。", plan });
        if (!Guid.TryParse(plan.WorkflowId, out var workflowId) || !plan.WorkflowVersion.HasValue)
            return UnprocessableEntity(new { message = "Agent Plan 未绑定有效 Workflow。" });
        var workflow = await db.Workflows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == workflowId, ct);
        if (workflow is null || workflow.BusinessFunctionId != plan.FunctionId || workflow.Status != AgentRPA.Domain.Workflow.WorkflowStatus.Published)
            return UnprocessableEntity(new { message = "Workflow 已失效或与业务功能不匹配。" });
        var version = await db.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(x => x.WorkflowId == workflowId && x.Version == plan.WorkflowVersion.Value && x.Published, ct);
        if (version is null) return UnprocessableEntity(new { message = "WorkflowVersion 未发布或已失效。" });
        var stepsAllowed = await stepPermissions.CheckAsync(subjectId, plan.CityId, plan.SystemId, plan.FunctionId, version.DefinitionJson, ct);
        if (!stepsAllowed.Allowed) return StatusCode(StatusCodes.Status403Forbidden, stepsAllowed);
        var task = new RpaTask(workflowId, version.Version, $"Agent: {plan.Action}", subjectId: subjectId);
        task.AddItem(System.Text.Json.JsonSerializer.Serialize(plan.Parameters));
        var requiresApproval = plan.RequiresConfirmation || WorkflowApprovalPolicy.RequiresApproval(version.DefinitionJson);
        if (!requiresApproval) task.Queue();
        db.Tasks.Add(task);
        TaskApprovalGate.AddPending(db, task, subjectId, version.DefinitionJson, plan.RequiresConfirmation);
        await db.SaveChangesAsync(ct);
        return Accepted($"api/tasks/{task.Id}", new { task.Id, plan, approvalStatus = requiresApproval ? "Pending" : null,
            message = requiresApproval ? "任务已创建，等待另一名管理员审批，获批后由任务所有者入队。" : "任务已通过权限检查并进入执行队列。" });
    }
}

public sealed record AgentPlanRequest(string Instruction);
public sealed record AgentPermissionCheckRequest(Guid CityId, Guid SystemId, Guid FunctionId, string Action);
public sealed record AgentExecuteRequest(string Instruction, bool Confirmed = false);
