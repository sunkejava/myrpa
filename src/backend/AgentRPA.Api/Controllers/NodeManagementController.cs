using Microsoft.AspNetCore.Authorization;
using AgentRPA.Domain.Execution;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>执行资源管理接口：节点、节点池、能力与 WorkerSlot。</summary>
[ApiController, Route("api/node-management"), Authorize(Roles = "Admin")]
public sealed class NodeManagementController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet("nodes")]
    public async Task<IActionResult> Nodes(CancellationToken ct) => Ok(await db.ExecutionNodes.AsNoTracking()
        .Select(x => new { x.Id, x.Name, x.AgentKey, NodeKind = x.NodeKind.ToString(), OsPlatform = x.OsPlatform.ToString(), Status = x.Status.ToString(), x.NodePoolId, x.NetworkZone, x.AgentVersion, x.LastHeartbeatAt,
            Capabilities = x.Capabilities.Where(c => c.Enabled).Select(c => new { c.Code, c.Version, c.MetadataJson }) })
        .ToListAsync(ct));

    [HttpGet("nodes/{id:guid}")]
    public async Task<IActionResult> Node(Guid id, CancellationToken ct)
    {
        var node = await db.ExecutionNodes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (node is null) return NotFound();
        var slots = await db.WorkerSlots.AsNoTracking().Where(x => x.NodeId == id).Select(x => new { x.Id, x.SlotName, x.Enabled, x.ExecutionId, x.LeaseExpiresAt }).ToListAsync(ct);
        var capabilities = await db.NodeCapabilities.AsNoTracking().Where(x => x.NodeId == id).Select(x => new { x.Id, x.Code, x.Version, x.Enabled, x.MetadataJson }).ToListAsync(ct);
        return Ok(new { node.Id, node.Name, node.AgentKey, NodeKind = node.NodeKind.ToString(), OsPlatform = node.OsPlatform.ToString(), Status = node.Status.ToString(), node.Architecture, node.NodePoolId, node.NetworkZone, node.AgentVersion, node.LastHeartbeatAt, capabilities, slots });
    }

    [HttpGet("pools")]
    public async Task<IActionResult> Pools(CancellationToken ct) => Ok(await db.NodePools.AsNoTracking()
        .Select(x => new { x.Id, x.Name, x.Description, x.Enabled, NodeCount = db.ExecutionNodes.Count(n => n.NodePoolId == x.Id) }).ToListAsync(ct));

    [HttpPost("pools")]
    public async Task<IActionResult> CreatePool(CreateNodePoolRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("节点池名称不能为空。");
        if (await db.NodePools.AnyAsync(x => x.Name == request.Name.Trim(), ct)) return Conflict("节点池名称已存在。");
        var pool = new NodePool(request.Name.Trim(), request.Description?.Trim());
        db.NodePools.Add(pool); await db.SaveChangesAsync(ct);
        return Created($"/api/node-management/pools/{pool.Id}", new { pool.Id, pool.Name, pool.Description, pool.Enabled });
    }

    [HttpPut("pools/{id:guid}")]
    public async Task<IActionResult> UpdatePool(Guid id, UpdateNodePoolRequest request, CancellationToken ct)
    {
        var pool = await db.NodePools.FindAsync([id], ct); if (pool is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("节点池名称不能为空。");
        if (await db.NodePools.AnyAsync(x => x.Id != id && x.Name == request.Name.Trim(), ct)) return Conflict("节点池名称已存在。");
        pool.Update(request.Name.Trim(), request.Description?.Trim(), request.Enabled);
        await db.SaveChangesAsync(ct); return Ok(new { pool.Id, pool.Name, pool.Description, pool.Enabled });
    }

    [HttpDelete("pools/{id:guid}")]
    public async Task<IActionResult> DeletePool(Guid id, CancellationToken ct)
    {
        var pool = await db.NodePools.FindAsync([id], ct); if (pool is null) return NotFound();
        if (await db.ExecutionNodes.AnyAsync(x => x.NodePoolId == id, ct)) return Conflict("节点池仍被执行节点使用，不能删除。");
        db.NodePools.Remove(pool); await db.SaveChangesAsync(ct); return NoContent();
    }

    [HttpGet("slots")]
    public async Task<IActionResult> Slots(CancellationToken ct) => Ok(await db.WorkerSlots.AsNoTracking().Select(x => new { x.Id, x.NodeId, x.SlotName, x.Enabled, x.ExecutionId, x.LeaseExpiresAt }).ToListAsync(ct));

    [HttpPost("slots/{id:guid}/enabled")]
    public async Task<IActionResult> SetSlotEnabled(Guid id, SetWorkerSlotEnabledRequest request, CancellationToken ct)
    {
        var slot = await db.WorkerSlots.FindAsync([id], ct); if (slot is null) return NotFound();
        if (slot.ExecutionId.HasValue && !request.Enabled) return Conflict("WorkerSlot 正在执行任务，不能直接禁用，请先等待租约结束。");
        slot.SetEnabled(request.Enabled); await db.SaveChangesAsync(ct);
        return Ok(new { slot.Id, slot.SlotName, slot.Enabled });
    }

    [HttpPost("nodes/{id:guid}/drain")]
    public async Task<IActionResult> Drain(Guid id, CancellationToken ct) { var node = await db.ExecutionNodes.FindAsync([id], ct); if (node is null) return NotFound(); if (node.Status is NodeStatus.PendingApproval or NodeStatus.Rejected or NodeStatus.Revoked) return Conflict(new { message = "节点尚未获准执行或身份已失效。" }); node.SetStatus(NodeStatus.Draining); await db.SaveChangesAsync(ct); return Ok(); }
    [HttpPost("nodes/{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct) { var node = await db.ExecutionNodes.FindAsync([id], ct); if (node is null) return NotFound(); if (node.Status is NodeStatus.PendingApproval or NodeStatus.Rejected or NodeStatus.Revoked) return Conflict(new { message = "节点尚未获准执行或身份已失效。" }); node.SetStatus(NodeStatus.Disabled); await db.SaveChangesAsync(ct); return Ok(); }
}

public sealed record CreateNodePoolRequest(string Name, string? Description);
public sealed record UpdateNodePoolRequest(string Name, string? Description, bool Enabled = true);
public sealed record SetWorkerSlotEnabledRequest(bool Enabled);
