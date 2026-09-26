using Microsoft.AspNetCore.Authorization;
using AgentRPA.Application.Nodes;
using AgentRPA.Application.Execution;
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
    IConfiguration configuration,
    IArtifactStorage artifactStorage) : ControllerBase
{
    /// <summary>节点将执行产物上传到服务端存储；客户端不能指定服务器路径。</summary>
    [HttpPost("{nodeId:guid}/executions/{executionId:guid}/artifacts"), RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadArtifact(Guid nodeId, Guid executionId,
        [FromQuery] Guid workerSlotId, [FromQuery] string artifactType, [FromQuery] string fileName,
        [FromQuery] string sha256, CancellationToken cancellationToken)
    {
        if (!await ValidateAgentKeyAsync(nodeId, cancellationToken)) return Unauthorized();
        if (!await db.ExecutionNodes.AnyAsync(x => x.Id == nodeId &&
            (x.Status == NodeStatus.Online || x.Status == NodeStatus.Draining), cancellationToken)) return StatusCode(403);
        var execution = await db.Executions.SingleOrDefaultAsync(x => x.Id == executionId && x.NodeId == nodeId && x.WorkerSlotId == workerSlotId, cancellationToken);
        if (execution is null || execution.Status is AgentRPA.Domain.Tasks.ExecutionStatus.Succeeded or AgentRPA.Domain.Tasks.ExecutionStatus.Failed or AgentRPA.Domain.Tasks.ExecutionStatus.Cancelled) return NotFound();
        if (string.IsNullOrWhiteSpace(artifactType) || artifactType.Length > 64 || string.IsNullOrWhiteSpace(fileName) ||
            fileName.Length > 260 || Path.GetFileName(fileName) != fileName ||
            sha256.Length != 64 || !sha256.All(Uri.IsHexDigit) ||
            Request.ContentLength is null or < 0 or > 50L * 1024 * 1024)
            return BadRequest(new { message = "产物元数据或文件大小无效。" });
        var storageKey = $"executions/{executionId:N}/{Guid.NewGuid():N}";
        try
        {
            await artifactStorage.StoreAsync(storageKey, Request.Body, cancellationToken);
            await using var stream = await artifactStorage.OpenReadAsync(storageKey, cancellationToken);
            var length = stream.Length;
            var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
            if (length != Request.ContentLength || !string.Equals(actualHash, sha256, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "产物内容长度或 SHA256 校验失败。" });
            db.ExecutionArtifacts.Add(new ExecutionArtifact(executionId, execution.TaskItemId, artifactType, fileName,
                storageKey, Request.ContentType, length, actualHash));
            await db.SaveChangesAsync(cancellationToken);
            return Ok(new { storageKey, sha256 = actualHash });
        }
        finally
        {
            if (!await db.ExecutionArtifacts.AnyAsync(x => x.StorageKey == storageKey, CancellationToken.None))
                await artifactStorage.DeleteAsync(storageKey, CancellationToken.None);
        }
    }
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
        ExecutionNode node;
        try { node = await nodeRegistry.RegisterAsync(registration, cancellationToken); }
        catch (InvalidOperationException) { return StatusCode(StatusCodes.Status403Forbidden, new { message = "节点身份已吊销，请更换 AgentKey 重新申请。" }); }
        return Ok(new NodeRegistrationResponse(node.Id, node.AgentKey, node.Status.ToString()));
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(NodeHeartbeatRequest request, CancellationToken cancellationToken)
    {
        if (!await ValidateAgentKeyAsync(request.NodeId, cancellationToken)) return Unauthorized(new { message = "节点身份认证失败。" });
        if (!double.IsFinite(request.CpuUsage) || request.CpuUsage is < 0 or > 100 ||
            !double.IsFinite(request.MemoryUsage) || request.MemoryUsage is < 0 or > 1_000_000 ||
            request.AvailableSlots is < 0 or > 100_000)
            return BadRequest(new { message = "运行指标无效。" });
        var accepted = await nodeRegistry.HeartbeatAsync(request, DateTimeOffset.UtcNow, cancellationToken);
        return accepted ? Ok(new NodeHeartbeatAck(request.NodeId, DateTimeOffset.UtcNow, "Accepted")) : NotFound();
    }

    /// <summary>管理员审批、禁用或恢复节点。PendingApproval 节点不会进入调度候选。</summary>
    [Authorize(Roles = "Admin"), HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, SetNodeStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<NodeStatus>(request.Status, true, out var status) ||
            status is not (NodeStatus.Draining or NodeStatus.Disabled or NodeStatus.Online))
            return BadRequest(new { message = "仅允许设为 Draining、Disabled 或 Online；审批、拒绝和吊销须使用专用接口。" });

        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NotFound();
        if (node.Status is NodeStatus.Rejected or NodeStatus.Revoked or NodeStatus.PendingApproval)
            return Conflict(new { message = "未审批、已拒绝或已吊销的节点不能通过状态接口启用。" });
        node.SetStatus(status);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { node.Id, status = node.Status.ToString() });
    }

    [Authorize(Roles = "Admin"), HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NotFound();
        if (node.Status != NodeStatus.PendingApproval)
            return Conflict(new { message = "只能审批待审批节点。" });
        node.Approve();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { node.Id, status = node.Status.ToString() });
    }

    [Authorize(Roles = "Admin"), HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NotFound();
        if (node.Status != NodeStatus.PendingApproval)
            return Conflict(new { message = "只能拒绝待审批节点。" });
        node.SetStatus(NodeStatus.Rejected);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { node.Id, status = node.Status.ToString() });
    }

    [Authorize(Roles = "Admin"), HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NotFound();
        if (node.Status == NodeStatus.Revoked) return Ok(new { node.Id, status = node.Status.ToString() });
        if (await db.Executions.AnyAsync(x => x.NodeId == id &&
            (x.Status == AgentRPA.Domain.Tasks.ExecutionStatus.Dispatched || x.Status == AgentRPA.Domain.Tasks.ExecutionStatus.Running ||
             x.Status == AgentRPA.Domain.Tasks.ExecutionStatus.Paused || x.Status == AgentRPA.Domain.Tasks.ExecutionStatus.WaitingForHuman), cancellationToken))
            return Conflict(new { message = "节点有未结束执行；请先排空节点并等待执行完成，再吊销身份。" });
        node.SetStatus(NodeStatus.Revoked);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { node.Id, status = node.Status.ToString() });
    }

    [Authorize(Roles = "Admin"), HttpPost("{id:guid}/drain")]
    public async Task<IActionResult> Drain(Guid id, CancellationToken cancellationToken)
    {
        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (node is null) return NotFound();
        if (node.Status is NodeStatus.PendingApproval or NodeStatus.Rejected or NodeStatus.Revoked)
            return Conflict(new { message = "节点尚未获准执行或身份已失效。" });
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
