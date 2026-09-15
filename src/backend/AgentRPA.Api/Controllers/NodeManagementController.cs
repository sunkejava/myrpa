using AgentRPA.Domain.Execution;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AgentRPA.Api.Controllers;
[ApiController, Route("api/node-management")]
public sealed class NodeManagementController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet("nodes")]
    public async Task<IActionResult> Nodes(CancellationToken ct) => Ok(await db.ExecutionNodes.AsNoTracking().Select(x => new { x.Id, x.Name, x.AgentKey, NodeKind = x.NodeKind.ToString(), OsPlatform = x.OsPlatform.ToString(), Status = x.Status.ToString(), x.NodePoolId, x.NetworkZone, x.LastHeartbeatAt }).ToListAsync(ct));
    [HttpGet("pools")]
    public async Task<IActionResult> Pools(CancellationToken ct) => Ok(await db.NodePools.AsNoTracking().Select(x => new { x.Id, x.Name, x.Description, x.Enabled, NodeCount = db.ExecutionNodes.Count(n => n.NodePoolId == x.Id) }).ToListAsync(ct));
    [HttpGet("slots")]
    public async Task<IActionResult> Slots(CancellationToken ct) => Ok(await db.WorkerSlots.AsNoTracking().Select(x => new { x.Id, x.NodeId, x.SlotName, x.Enabled, x.ExecutionId, x.LeaseExpiresAt }).ToListAsync(ct));
    [HttpPost("nodes/{id:guid}/drain")]
    public async Task<IActionResult> Drain(Guid id, CancellationToken ct) { var node = await db.ExecutionNodes.FindAsync([id], ct); if (node is null) return NotFound(); node.SetStatus(NodeStatus.Draining); await db.SaveChangesAsync(ct); return Ok(); }
    [HttpPost("nodes/{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct) { var node = await db.ExecutionNodes.FindAsync([id], ct); if (node is null) return NotFound(); node.SetStatus(NodeStatus.Disabled); await db.SaveChangesAsync(ct); return Ok(); }
}
