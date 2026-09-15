using AgentRPA.Domain.Workflow;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AgentRPA.Api.Controllers;
[ApiController, Route("api/workflows")]
public sealed class WorkflowsController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Workflows.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.BusinessFunctionId, Status = x.Status.ToString() }).ToListAsync(ct));
    [HttpPost] public async Task<IActionResult> Create(CreateWorkflowRequest request, CancellationToken ct) { var e = new Workflow(request.BusinessFunctionId, request.Name, request.Description); db.Workflows.Add(e); await db.SaveChangesAsync(ct); return Created($"api/workflows/{e.Id}", new { e.Id }); }
    [HttpPost("{id:guid}/publish")] public async Task<IActionResult> Publish(Guid id, CancellationToken ct) { var e = await db.Workflows.FindAsync([id], ct); if (e is null) return NotFound(); e.Publish(); await db.SaveChangesAsync(ct); return Ok(); }
}
public sealed record CreateWorkflowRequest(Guid BusinessFunctionId, string Name, string? Description);
