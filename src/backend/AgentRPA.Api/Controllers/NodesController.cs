using AgentRPA.Contracts.Nodes;
using Microsoft.AspNetCore.Mvc;

namespace AgentRPA.Api.Controllers;

/// <summary>执行节点控制面 API。真正的 Workflow 执行仍由 Node Agent 完成。</summary>
[ApiController]
[Route("api/nodes")]
public sealed class NodesController : ControllerBase
{
    /// <summary>节点首次注册。生产环境需要增加节点身份认证和注册审批。</summary>
    [HttpPost("register")]
    public IActionResult Register(RegisterNodeRequest request)
    {
        // 当前仅搭建控制面契约；后续由 Application 层持久化并签发 Node 身份。
        return Ok(new { nodeId = Guid.NewGuid(), status = "PendingRegistration" });
    }

    /// <summary>节点心跳。后续由 Application 层更新 Node 状态、能力和 WorkerSlot。</summary>
    [HttpPost("heartbeat")]
    public IActionResult Heartbeat(NodeHeartbeatRequest request)
    {
        return Ok(new { accepted = true, serverTime = DateTimeOffset.UtcNow });
    }
}
