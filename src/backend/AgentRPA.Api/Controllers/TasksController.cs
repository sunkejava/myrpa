using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AgentRPA.Api.Controllers;
[ApiController, Route("api/tasks")]
public sealed class TasksController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Tasks.AsNoTracking().OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.Name, x.WorkflowId, x.WorkflowVersion, Status = x.Status.ToString() }).ToListAsync(ct));
    [HttpPost] public async Task<IActionResult> Create(CreateTaskRequest request, CancellationToken ct) { var task = new RpaTask(request.WorkflowId, request.WorkflowVersion, request.Name); foreach (var item in request.Items ?? []) task.AddItem(item); db.Tasks.Add(task); await db.SaveChangesAsync(ct); return Created($"api/tasks/{task.Id}", new { task.Id }); }
    [HttpPost("{id:guid}/queue")] public async Task<IActionResult> Queue(Guid id, CancellationToken ct) { var task = await db.Tasks.FindAsync([id], ct); if (task is null) return NotFound(); task.Queue(); await db.SaveChangesAsync(ct); return Ok(); }
}
public sealed record CreateTaskRequest(Guid WorkflowId, int WorkflowVersion, string Name, IReadOnlyCollection<string>? Items);
