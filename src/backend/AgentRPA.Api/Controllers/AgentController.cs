using AgentRPA.Application.Agent;
using AgentRPA.Application.Permission;
using Microsoft.AspNetCore.Mvc;

namespace AgentRPA.Api.Controllers;

/// <summary>自然语言 Agent 规划接口。这里只生成计划，不直接执行外部业务系统。</summary>
[ApiController, Route("api/agent")]
public sealed class AgentController(AgentPlanningService planner, PermissionService permissionService) : ControllerBase
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
}

public sealed record AgentPlanRequest(string Instruction);
public sealed record AgentPermissionCheckRequest(Guid SubjectId, Guid CityId, Guid SystemId, Guid FunctionId, string Action);
