using AgentRPA.Application.Identity;
using AgentRPA.Domain.Identity;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>管理员管理登录账户和角色；响应不包含密码散列。</summary>
[ApiController, Route("api/user-management"), Authorize(Roles = "Admin")]
public sealed class UserManagementController(AgentRpaDbContext db, IIdentityService identities) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct) => Ok(await db.UserAccounts.AsNoTracking()
        .OrderBy(x => x.UserName).Select(x => new { x.Id, x.UserName, x.DisplayName, x.Enabled }).ToListAsync(ct));

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        try
        {
            var user = await identities.CreateUserAsync(request.UserName, request.DisplayName, request.Password, ct);
            return Created($"/api/user-management/users/{user.Id}", new { user.Id, user.UserName, user.DisplayName, user.Enabled });
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("users/{id:guid}/enabled")]
    public async Task<IActionResult> SetUserEnabled(Guid id, SetAccountEnabledRequest request, CancellationToken ct)
    {
        var user = await db.UserAccounts.FindAsync([id], ct);
        if (user is null) return NotFound();
        if (!request.Enabled && User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == id.ToString())
            return Conflict(new { message = "不能停用当前登录账户。" });
        user.SetEnabled(request.Enabled);
        await db.SaveChangesAsync(ct);
        return Ok(new { user.Id, user.Enabled });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken ct) => Ok(await db.Roles.AsNoTracking()
        .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.DisplayName, x.Enabled }).ToListAsync(ct));

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole(CreateRoleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 64 || request.DisplayName.Length > 128)
            return BadRequest(new { message = "角色名称无效。" });
        var name = request.Name.Trim();
        if (await db.Roles.AnyAsync(x => x.Name == name, ct)) return Conflict(new { message = "角色已存在。" });
        var role = new Role(name, request.DisplayName);
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return Created($"/api/user-management/roles/{role.Id}", new { role.Id, role.Name, role.DisplayName });
    }

    [HttpPost("users/{id:guid}/roles/{roleId:guid}")]
    public async Task<IActionResult> AssignRole(Guid id, Guid roleId, CancellationToken ct)
    {
        var user = await db.UserAccounts.Include(x => x.Roles).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null || !await db.Roles.AnyAsync(x => x.Id == roleId && x.Enabled, ct)) return NotFound();
        user.AddRole(roleId);
        await db.SaveChangesAsync(ct);
        return Ok(new { user.Id, RoleId = roleId });
    }
}

public sealed record CreateUserRequest(string UserName, string DisplayName, string Password);
public sealed record CreateRoleRequest(string Name, string DisplayName);
public sealed record SetAccountEnabledRequest(bool Enabled);
