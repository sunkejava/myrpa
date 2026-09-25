using AgentRPA.Domain.Identity;
using AgentRPA.Domain.Permission;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>角色的精确资源授权和显式拒绝；用户 Deny 可压过角色 Allow。</summary>
[ApiController, Route("api/role-policies"), Authorize(Roles = "Admin")]
public sealed class RolePoliciesController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? roleId, CancellationToken ct) => Ok(await db.RoleAccessPolicies.AsNoTracking()
        .Where(x => !roleId.HasValue || x.RoleId == roleId.Value)
        .OrderBy(x => x.RoleId).ThenBy(x => x.Action)
        .Select(x => new { x.Id, x.RoleId, x.CityId, x.SystemId, x.FunctionId, x.Action, x.Enabled, x.Denied })
        .ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Set(SetRolePolicyRequest request, CancellationToken ct)
    {
        if (request.RoleId == Guid.Empty || request.CityId == Guid.Empty || request.SystemId == Guid.Empty ||
            request.FunctionId == Guid.Empty || !PermissionScopeRules.IsValidAction(request.Action))
            return BadRequest(new { message = "角色、城市、系统、功能和动作必须完整指定。" });
        if (!await db.Roles.AnyAsync(x => x.Id == request.RoleId && x.Enabled, ct) ||
            !await db.Cities.AnyAsync(x => x.Id == request.CityId && x.Enabled, ct) ||
            !await db.BusinessSystems.AnyAsync(x => x.Id == request.SystemId && x.CityId == request.CityId && x.Enabled, ct) ||
            !await db.BusinessFunctions.AnyAsync(x => x.Id == request.FunctionId && x.SystemId == request.SystemId, ct))
            return BadRequest(new { message = "角色或城市、系统、功能的资源关系无效。" });
        var action = request.Action.Trim();
        var policy = await db.RoleAccessPolicies.SingleOrDefaultAsync(x => x.RoleId == request.RoleId &&
            x.CityId == request.CityId && x.SystemId == request.SystemId && x.FunctionId == request.FunctionId && x.Action == action, ct);
        if (policy is null)
        {
            policy = new RoleAccessPolicy(request.RoleId, request.CityId, request.SystemId, request.FunctionId, action, request.Denied);
            db.RoleAccessPolicies.Add(policy);
        }
        else { policy.SetDenied(request.Denied); policy.SetEnabled(true); }
        await db.SaveChangesAsync(ct);
        return Ok(new { policy.Id, policy.RoleId, policy.CityId, policy.SystemId, policy.FunctionId, policy.Action, policy.Enabled, policy.Denied });
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var policy = await db.RoleAccessPolicies.FindAsync([id], ct);
        if (policy is null) return NotFound();
        policy.SetEnabled(false);
        await db.SaveChangesAsync(ct);
        return Ok(new { policy.Id, policy.Enabled });
    }
}

public sealed record SetRolePolicyRequest(Guid RoleId, Guid CityId, Guid SystemId, Guid FunctionId, string Action, bool Denied = false);
