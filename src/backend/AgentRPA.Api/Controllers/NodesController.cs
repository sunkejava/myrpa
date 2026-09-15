using AgentRPA.Application.Nodes;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Domain.Execution;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>执行节点控制面 API。真正的 Workflow 执行仍由 Node Agent 完成。</summary>
[ApiController]
[Route("api/nodes")]
public sealed class NodesController(
    INodeRegistryService nodeRegistry,
    AgentRpaDbContext db) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterNodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AgentKey) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "AgentKey 和节点名称不能为空。" });
        if (!Enum.TryParse<NodeKind>(request.NodeKind, true, out var nodeKind) ||
            !Enum.TryParse<OsPlatform>(request.OsPlatform, true, out var osPlatform))
            return BadRequest(new { message = "NodeKind 或 OsPlatform 无效。" });

        var registration = new NodeRegistration(
            request.AgentKey.Trim(), request.Name.Trim(), nodeKind, osPlatform, request.Architecture,
            request.NetworkZone, request.NodePoolId, request.AgentVersion,
            request.Capabilities.Select(x => new NodeCapabilityInput(x.Code, x.Version, x.MetadataJson)).ToArray(),
            request.WorkerSlots);
        var node = await nodeRegistry.RegisterAsync(registration, cancellationToken);
        return Ok(new NodeRegistrationResponse(node.Id, node.AgentKey, node.Status.ToString()));
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(NodeHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var accepted = await nodeRegistry.HeartbeatAsync(request.NodeId, request.AgentVersion, request.SentAt, cancellationToken);
        return accepted ? Ok(new NodeHeartbeatAck(request.NodeId, DateTimeOffset.UtcNow, "Accepted")) : NotFound();
    }

    /// <summary>管理员审批、禁用或恢复节点。PendingApproval 节点不会进入调度候选。</summary>
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, SetNodeStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<NodeStatus>(request.Status, true, out var status))
            return BadRequest(new { message = "节点状态无效。" });

        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NotFound();
        node.SetStatus(status);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { node.Id, status = node.Status.ToString() });
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NotFound();
        node.Approve();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { node.Id, status = node.Status.ToString() });
    }

    [HttpPost("{id:guid}/drain")]
    public async Task<IActionResult> Drain(Guid id, CancellationToken cancellationToken)
    {
        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NotFound();
        node.SetStatus(NodeStatus.Draining);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { node.Id, status = node.Status.ToString() });
    }
}

public sealed record SetNodeStatusRequest(string Status);
