using AgentRPA.Application.Agent;
using AgentRPA.Application.Permission;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>自然语言 Agent 控制器：规划、权限预检查以及经确认后的 Task 创建。</summary>
[ApiController, Route("api/agent")]
public sealed class AgentController(AgentPlanningService planner, PermissionService permissionService, AgentRpaDbContext db) : ControllerBase
{
    [HttpPost("plan")]
    public async Task<IActionResult> Plan(AgentPlanRequest request, CancellationToken ct)
    {
        var result = await planner.PlanAsync(request.Instruction, ct);
        return result.Success ? Ok(result) : UnprocessableEntity(result);
    }

    /// <summary>执行前权限预检查。真正的 Task/Execution 派发前仍必须再次校验。</summary>
    [HttpPost("check-permission")]
    public async Task<IActionResult> CheckPermission(AgentPermissionCheckRequest request, CancellationToken ct)
    {
        var result = await permissionService.CheckAsync(request.SubjectId, request.CityId, request.SystemId, request.FunctionId, request.Action, ct);
        return result.Allowed ? Ok(result) : StatusCode(StatusCodes.Status403Forbidden, result);
    }

    /// <summary>自然语言 → Plan → Permission → Confirmation → Task。Agent 不直接操作浏览器。</summary>
    [HttpPost("execute")]
    public async Task<IActionResult> Execute(AgentExecuteRequest request, CancellationToken ct)
    {
        var planResult = await planner.PlanAsync(request.Instruction, ct);
        if (!planResult.Success || planResult.Plan is null) return UnprocessableEntity(planResult);
        var plan = planResult.Plan;

        var permission = await permissionService.CheckAsync(request.SubjectId, plan.CityId, plan.SystemId, plan.FunctionId, plan.Action, ct);
        if (!permission.Allowed) return StatusCode(StatusCodes.Status403Forbidden, permission);

        if (plan.RequiresConfirmation && !request.Confirmed)
            return StatusCode(StatusCodes.Status428PreconditionRequired, new
            {
                confirmationRequired = true,
                message = "该操作需要用户明确确认后才能创建执行任务。",
                plan
            });

        if (!Guid.TryParse(plan.WorkflowId, out var workflowId) || !plan.WorkflowVersion.HasValue)
            return UnprocessableEntity(new { message = "Agent Plan 未绑定有效 Workflow。" });

        var workflow = await db.Workflows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == workflowId, ct);
        if (workflow is null || workflow.BusinessFunctionId != plan.FunctionId || workflow.Status != AgentRPA.Domain.Workflow.WorkflowStatus.Published)
            return UnprocessableEntity(new { message = "Workflow 已失效或与业务功能不匹配。" });

        var version = await db.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(x => x.WorkflowId == workflowId && x.Version == plan.WorkflowVersion.Value && x.Published, ct);
        if (version is null) return UnprocessableEntity(new { message = "WorkflowVersion 未发布或已失效。" });

        var task = new RpaTask(workflowId, version.Version, $"Agent: {plan.Action}", subjectId: request.SubjectId);
        task.AddItem(System.Text.Json.JsonSerializer.Serialize(plan.Parameters));
        task.Queue();
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        return Accepted($"api/tasks/{task.Id}", new { task.Id, plan, message = "任务已通过权限与确认检查并进入执行队列。" });
    }
}

public sealed record AgentPlanRequest(string Instruction);
public sealed record AgentPermissionCheckRequest(Guid SubjectId, Guid CityId, Guid SystemId, Guid FunctionId, string Action);
public sealed record AgentExecuteRequest(Guid SubjectId, string Instruction, bool Confirmed = false);
