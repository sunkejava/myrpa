using AgentRPA.Application.Nodes;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Domain.Execution;
using Microsoft.AspNetCore.Mvc;

namespace AgentRPA.Api.Controllers;

/// <summary>执行节点控制面 API。真正的 Workflow 执行仍由 Node Agent 完成。</summary>
[ApiController]
[Route("api/nodes")]
public sealed class NodesController(INodeRegistryService nodeRegistry) : ControllerBase
{
    /// <summary>节点注册。生产环境后续接入节点密钥/证书认证和管理员审批。</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterNodeRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<NodeKind>(request.NodeKind, true, out var nodeKind) ||
            !Enum.TryParse<OsPlatform>(request.OsPlatform, true, out var osPlatform))
            return BadRequest(new { message = "NodeKind 或 OsPlatform 无效。" });

        var registration = new NodeRegistration(
            request.AgentKey, request.Name, nodeKind, osPlatform, request.Architecture,
            request.NetworkZone, request.NodePoolId, request.AgentVersion,
            request.Capabilities.Select(x => new NodeCapabilityInput(x.Code, x.Version, x.MetadataJson)).ToArray(),
            request.WorkerSlots);
        var node = await nodeRegistry.RegisterAsync(registration, cancellationToken);
        return Ok(new { nodeId = node.Id, agentKey = node.AgentKey, status = node.Status.ToString() });
    }

    /// <summary>节点心跳。服务端以最近心跳判断调度资格。</summary>
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(NodeHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var accepted = await nodeRegistry.HeartbeatAsync(request.NodeId, request.AgentVersion, request.SentAt, cancellationToken);
        return accepted ? Ok(new { accepted = true, serverTime = DateTimeOffset.UtcNow }) : NotFound();
    }
}
