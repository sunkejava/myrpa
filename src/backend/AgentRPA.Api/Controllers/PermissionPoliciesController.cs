using AgentRPA.Api.Security;
using AgentRPA.Application.Permission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentRPA.Api.Controllers;

/// <summary>细粒度权限策略管理。管理接口要求管理员角色；业务执行接口仍通过 PermissionService 精确校验。</summary>
[ApiController, Route("api/permission-policies"), Authorize(Roles = "Admin")]
public sealed class PermissionPoliciesController(PermissionManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? subjectId, CancellationToken ct)
        => Ok(await service.ListAsync(subjectId, ct));

    [HttpPost]
    public async Task<IActionResult> Grant(GrantPermissionRequest request, CancellationToken ct)
    {
        try
        {
            var policy = await service.GrantAsync(request.SubjectId, request.CityId, request.SystemId, request.FunctionId, request.Action, ct);
            return Ok(new { policy.Id, policy.SubjectId, policy.CityId, policy.SystemId, policy.FunctionId, policy.Action, policy.Enabled });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
        => await service.RevokeAsync(id, ct) ? Ok(new { id, enabled = false }) : NotFound();
}

public sealed record GrantPermissionRequest(Guid SubjectId, Guid CityId, Guid SystemId, Guid FunctionId, string Action);
