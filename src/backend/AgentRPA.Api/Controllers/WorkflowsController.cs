using AgentRPA.Application.Workflow;
using AgentRPA.Domain.Workflow;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AgentRPA.Api.Controllers;

[ApiController, Route("api/workflows")]
public sealed class WorkflowsController(AgentRpaDbContext db, WorkflowDefinitionValidator validator) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Workflows.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.BusinessFunctionId, Status = x.Status.ToString() }).ToListAsync(ct));
    [HttpPost] public async Task<IActionResult> Create(CreateWorkflowRequest request, CancellationToken ct) { var e = new Workflow(request.BusinessFunctionId, request.Name, request.Description); db.Workflows.Add(e); await db.SaveChangesAsync(ct); return Created($"api/workflows/{e.Id}", new { e.Id }); }
    [HttpGet("{id:guid}/versions")] public async Task<IActionResult> Versions(Guid id, CancellationToken ct) => Ok(await db.WorkflowVersions.AsNoTracking().Where(x => x.WorkflowId == id).OrderByDescending(x => x.Version).Select(x => new { x.Id, x.Version, x.Published, x.CreatedAt }).ToListAsync(ct));
    [HttpPost("{id:guid}/versions")] public async Task<IActionResult> CreateVersion(Guid id, CreateWorkflowVersionRequest request, CancellationToken ct)
    {
        if (!await db.Workflows.AnyAsync(x => x.Id == id, ct)) return NotFound();
        var errors = validator.Validate(request.DefinitionJson);
        if (errors.Count > 0) return BadRequest(new { message = "Workflow 定义校验失败。", errors });
        var number = await db.WorkflowVersions.Where(x => x.WorkflowId == id).MaxAsync(x => (int?)x.Version, ct) ?? 0;
        var entity = new WorkflowVersion(id, number + 1, request.DefinitionJson);
        db.WorkflowVersions.Add(entity); await db.SaveChangesAsync(ct);
        return Created($"api/workflows/{id}/versions/{entity.Version}", new { entity.Id, entity.Version });
    }
    [HttpGet("{id:guid}/versions/{version:int}")] public async Task<IActionResult> GetVersion(Guid id, int version, CancellationToken ct)
    {
        var entity = await db.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(x => x.WorkflowId == id && x.Version == version, ct);
        return entity is null ? NotFound() : Ok(new { entity.Id, entity.WorkflowId, entity.Version, entity.Published, entity.DefinitionJson });
    }
    [HttpPost("{id:guid}/versions/{version:int}/publish")] public async Task<IActionResult> PublishVersion(Guid id, int version, CancellationToken ct)
    {
        var entity = await db.WorkflowVersions.SingleOrDefaultAsync(x => x.WorkflowId == id && x.Version == version, ct);
        if (entity is null) return NotFound();
        var errors = validator.Validate(entity.DefinitionJson);
        if (errors.Count > 0) return BadRequest(new { message = "Workflow 定义校验失败。", errors });
        entity.Publish(); var workflow = await db.Workflows.SingleAsync(x => x.Id == id, ct); workflow.Publish();
        await db.SaveChangesAsync(ct); return Ok(new { entity.Id, entity.Version, entity.Published });
    }
    [HttpPost("{id:guid}/publish")] public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        if (!await db.WorkflowVersions.AnyAsync(x => x.WorkflowId == id && x.Published, ct)) return BadRequest(new { message = "Workflow 至少需要一个已发布版本。" });
        var e = await db.Workflows.FindAsync([id], ct); if (e is null) return NotFound(); e.Publish(); await db.SaveChangesAsync(ct); return Ok();
    }
}
public sealed record CreateWorkflowRequest(Guid BusinessFunctionId, string Name, string? Description);
public sealed record CreateWorkflowVersionRequest(string DefinitionJson);
