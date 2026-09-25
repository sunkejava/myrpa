using Microsoft.AspNetCore.Authorization;
using AgentRPA.Application.Nodes;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Domain.Execution;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AgentRPA.Api.Controllers;

/// <summary>执行节点控制面 API。真正的 Workflow 执行仍由 Node Agent 完成。</summary>
[ApiController]
[Route("api/nodes")]
public sealed class NodesController(
    INodeRegistryService nodeRegistry,
    AgentRpaDbContext db,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterNodeRequest request, CancellationToken cancellationToken)
    {
        // 首次注册使用独立 Bootstrap Key，避免任意客户端伪造 AgentKey 创建节点身份。
        if (!ValidateBootstrapKey()) return Unauthorized(new { message = "节点注册密钥无效。" });
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
        if (!await ValidateAgentKeyAsync(request.NodeId, cancellationToken)) return Unauthorized(new { message = "节点身份认证失败。" });
        var accepted = await nodeRegistry.HeartbeatAsync(request.NodeId, request.AgentVersion, request.SentAt, cancellationToken);
        return accepted ? Ok(new NodeHeartbeatAck(request.NodeId, DateTimeOffset.UtcNow, "Accepted")) : NotFound();
    }

    /// <summary>管理员审批、禁用或恢复节点。PendingApproval 节点不会进入调度候选。</summary>
    [Authorize(Roles = "Admin"), HttpPost("{id:guid}/status")]
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

    [Authorize(Roles = "Admin"), HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NotFound();
        node.Approve();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { node.Id, status = node.Status.ToString() });
    }

    [Authorize(Roles = "Admin"), HttpPost("{id:guid}/drain")]
    public async Task<IActionResult> Drain(Guid id, CancellationToken cancellationToken)
    {
        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NotFound();
        node.SetStatus(NodeStatus.Draining);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { node.Id, status = node.Status.ToString() });
    }

    private bool ValidateBootstrapKey()
    {
        var expected = configuration["NodeAuthentication:RegistrationKey"];
        return !string.IsNullOrWhiteSpace(expected)
            && Request.Headers.TryGetValue("X-Node-Registration-Key", out var supplied)
            && supplied.Count == 1
            && FixedEquals(expected, supplied[0]);
    }

    private async Task<bool> ValidateAgentKeyAsync(Guid nodeId, CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Agent-Key", out var supplied) || supplied.Count != 1) return false;
        var expected = await db.ExecutionNodes.AsNoTracking()
            .Where(x => x.Id == nodeId)
            .Select(x => x.AgentKey)
            .SingleOrDefaultAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(expected) && FixedEquals(expected, supplied[0]);
    }

    private static bool FixedEquals(string expected, string? supplied)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied ?? string.Empty));
}

public sealed record SetNodeStatusRequest(string Status);
