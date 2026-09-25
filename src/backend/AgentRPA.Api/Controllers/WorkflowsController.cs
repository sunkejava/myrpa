using Microsoft.AspNetCore.Authorization;
using AgentRPA.Application.Workflow;
using AgentRPA.Domain.Workflow;
using AgentRPA.Infrastructure.Persistence;
using AgentRPA.Api.Hubs;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Domain.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
namespace AgentRPA.Api.Controllers;

[ApiController, Route("api/workflows"), Authorize]
public sealed class WorkflowsController(AgentRpaDbContext db, WorkflowDefinitionValidator validator,
    NodeAgentConnectionRegistry connections, IHubContext<NodeAgentHub, INodeAgentClient> hub,
    ILogger<WorkflowsController> logger) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Workflows.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.BusinessFunctionId, Status = x.Status.ToString() }).ToListAsync(ct));
    [Authorize(Roles = "Admin"), HttpPost] public async Task<IActionResult> Create(CreateWorkflowRequest request, CancellationToken ct)
    {
        if (request.BusinessFunctionId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            return BadRequest(new { message = "业务功能和 Workflow 名称必须有效。" });
        if (!await db.BusinessFunctions.AnyAsync(f => f.Id == request.BusinessFunctionId &&
            db.BusinessSystems.Any(s => s.Id == f.SystemId && s.Enabled && db.Cities.Any(c => c.Id == s.CityId && c.Enabled &&
                (!c.ProvinceId.HasValue || db.Provinces.Any(p => p.Id == c.ProvinceId && p.Enabled &&
                    db.Countries.Any(country => country.Id == p.CountryId && country.Enabled))))), ct))
            return BadRequest(new { message = "业务功能不存在或其区域、系统已停用。" });
        var e = new Workflow(request.BusinessFunctionId, request.Name.Trim(), request.Description);
        db.Workflows.Add(e); await db.SaveChangesAsync(ct);
        return Created($"api/workflows/{e.Id}", new { e.Id });
    }
    [HttpGet("{id:guid}/versions")] public async Task<IActionResult> Versions(Guid id, CancellationToken ct) => Ok(await db.WorkflowVersions.AsNoTracking().Where(x => x.WorkflowId == id).OrderByDescending(x => x.Version).Select(x => new { x.Id, x.Version, x.Published, x.CreatedAt }).ToListAsync(ct));
    [Authorize(Roles = "Admin"), HttpPost("{id:guid}/versions")] public async Task<IActionResult> CreateVersion(Guid id, CreateWorkflowVersionRequest request, CancellationToken ct)
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
    [Authorize(Roles = "Admin"), HttpPost("{id:guid}/versions/{version:int}/publish")] public async Task<IActionResult> PublishVersion(Guid id, int version, CancellationToken ct)
    {
        var entity = await db.WorkflowVersions.SingleOrDefaultAsync(x => x.WorkflowId == id && x.Version == version, ct);
        if (entity is null) return NotFound();
        var errors = validator.Validate(entity.DefinitionJson);
        if (errors.Count > 0) return BadRequest(new { message = "Workflow 定义校验失败。", errors });
        entity.Publish(); var workflow = await db.Workflows.SingleAsync(x => x.Id == id, ct); workflow.Publish();
        await db.SaveChangesAsync(ct); return Ok(new { entity.Id, entity.Version, entity.Published });
    }
    [Authorize(Roles = "Admin"), HttpPost("{id:guid}/publish")] public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        if (!await db.WorkflowVersions.AnyAsync(x => x.WorkflowId == id && x.Published, ct)) return BadRequest(new { message = "Workflow 至少需要一个已发布版本。" });
        var e = await db.Workflows.FindAsync([id], ct); if (e is null) return NotFound(); e.Publish(); await db.SaveChangesAsync(ct); return Ok();
    }
    [Authorize(Roles = "Admin"), HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct)
    {
        var workflow = await db.Workflows.FindAsync([id], ct);
        if (workflow is null) return NotFound();
        workflow.Disable(); await db.SaveChangesAsync(ct);
        // Stop active nodes after the persisted state change. If a node is offline, the StepStarted
        // gate in NodeAgentHub will stop its next step when it reconnects.
        var active = await (from execution in db.Executions.AsNoTracking()
            join item in db.TaskItems.AsNoTracking() on execution.TaskItemId equals item.Id
            join task in db.Tasks.AsNoTracking() on item.TaskId equals task.Id
            where task.WorkflowId == id && execution.NodeId != null &&
                (execution.Status == ExecutionStatus.Dispatched || execution.Status == ExecutionStatus.Running ||
                 execution.Status == ExecutionStatus.Paused || execution.Status == ExecutionStatus.WaitingForHuman)
            select new { execution.Id, execution.NodeId }).ToListAsync(ct);
        var signaled = 0;
        foreach (var execution in active)
        {
            if (!connections.TryGet(execution.NodeId!.Value, out var connectionId) || connectionId is null) continue;
            try { await hub.Clients.Client(connectionId).CancelAsync(execution.Id); signaled++; }
            catch (Exception ex) { logger.LogWarning(ex, "Workflow {WorkflowId} 停用后通知 Execution {ExecutionId} 取消失败", id, execution.Id); }
        }
        return Ok(new { workflow.Id, status = workflow.Status.ToString(), activeExecutions = active.Count, signaled });
    }
}
public sealed record CreateWorkflowRequest(Guid BusinessFunctionId, string Name, string? Description);
public sealed record CreateWorkflowVersionRequest(string DefinitionJson);
