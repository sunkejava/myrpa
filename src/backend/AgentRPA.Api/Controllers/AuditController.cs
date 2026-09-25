using Microsoft.AspNetCore.Authorization;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>审计查询 API。敏感字段由业务层只记录摘要，不返回凭据明文。</summary>
[ApiController, Route("api/audit"), Authorize(Roles = "Admin")]
public sealed class AuditController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? actor, [FromQuery] string? resource, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var query = db.AuditEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(actor)) query = query.Where(x => x.Actor == actor);
        if (!string.IsNullOrWhiteSpace(resource)) query = query.Where(x => x.Resource == resource);
        return Ok(await query.OrderByDescending(x => x.Id).Take(limit)
            .Select(x => new { x.Id, x.CreatedAt, x.Actor, x.Action, x.Resource, x.ResourceId, x.Result, x.Summary })
            .ToListAsync(ct));
    }
}
