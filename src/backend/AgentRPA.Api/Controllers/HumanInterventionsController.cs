using System.Security.Cryptography;
using System.Text;
using AgentRPA.Api.Hubs;
using AgentRPA.Api.Security;
using AgentRPA.Contracts.Interventions;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Domain.HumanIntervention;
using AgentRPA.Domain.Tasks;
using AgentRPA.Domain.Workflow;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using DomainTaskStatus = AgentRPA.Domain.Tasks.TaskStatus;

namespace AgentRPA.Api.Controllers;

/// <summary>人工介入控制面 API，供验证码、扫码、人脸和 UKey 场景暂停/恢复执行。</summary>
[ApiController]
[Route("api/human-interventions")]
[Authorize]
public sealed class HumanInterventionsController(
    AgentRpaDbContext db,
    NodeAgentConnectionRegistry connections,
    IHubContext<NodeAgentHub, INodeAgentClient> hub) : ControllerBase
{
    private static readonly TimeSpan MaxQrLifetime = TimeSpan.FromMinutes(10);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HumanInterventionDto>>> List([FromQuery] Guid? executionId, CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId))
            return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });

        var query = from intervention in db.HumanInterventions.AsNoTracking()
                    where intervention.SubjectId == subjectId
                    select intervention;

        if (executionId.HasValue)
            query = query.Where(x => x.ExecutionId == executionId.Value);

        return Ok(await query
            .OrderByDescending(x => x.Id)
            .Select(x => new HumanInterventionDto(
                x.Id,
                x.ExecutionId,
                x.Type.ToString(),
                x.Status.ToString(),
                x.Title,
                x.ExpiresAt))
            .ToListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<HumanInterventionDto>> Create(CreateHumanInterventionRequest request, CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId))
            return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });

        if (!Enum.TryParse<InterventionType>(request.Type, true, out var type))
            return BadRequest(new { message = "不支持的人工介入类型。" });

        var execution = await GetOwnedExecutionAsync(request.ExecutionId, subjectId, cancellationToken);
        if (execution is null)
            return NotFound(new { message = "Execution 不存在或不属于当前用户。" });

        var now = DateTimeOffset.UtcNow;
        if (request.ExpiresAt <= now)
            return BadRequest(new { message = "人工介入过期时间必须晚于当前时间。" });

        if (type == InterventionType.QrLogin && request.ExpiresAt > now.Add(MaxQrLifetime))
            return BadRequest(new { message = "二维码授权有效期不能超过 10 分钟。" });

        var intervention = new HumanIntervention(request.ExecutionId, subjectId, type, request.Title, request.ExpiresAt);
        string? qrToken = null;
        if (type == InterventionType.QrLogin)
        {
            qrToken = CreateQrToken();
            intervention.Open(HashToken(qrToken));
        }
        else intervention.Open(null);

        db.HumanInterventions.Add(intervention);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(intervention, qrToken));
    }

    [HttpPost("{id:guid}/qr/consume")]
    public async Task<ActionResult<HumanInterventionDto>> ConsumeQrToken(Guid id, ConsumeQrTokenRequest request, CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId))
            return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 256)
            return BadRequest(new { message = "二维码授权令牌无效。" });

        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(request.Token.Trim());
        var intervention = await db.HumanInterventions.SingleOrDefaultAsync(x => x.Id == id && x.SubjectId == subjectId, cancellationToken);
        if (intervention is null) return NotFound();
        if (intervention.Type != InterventionType.QrLogin)
            return BadRequest(new { message = "该人工介入不是二维码授权。" });
        if (intervention.Status == InterventionStatus.Opened && now > intervention.ExpiresAt)
        {
            intervention.ExpireIfNeeded(now);
            await db.SaveChangesAsync(cancellationToken);
            return BadRequest(new { message = "二维码授权已过期。" });
        }
        if (intervention.Status != InterventionStatus.Opened)
            return BadRequest(new { message = "二维码授权令牌无效、已使用或已过期。" });

        // 有效期按本次请求捕获的 now 判定；SQLite 无法在 ExecuteUpdate 中翻译
        // DateTimeOffset 的大小比较。状态、摘要及未消费条件仍在单条 SQL 中原子判定。
        var affected = await db.HumanInterventions
            .Where(x => x.Id == id && x.SubjectId == subjectId && x.Type == InterventionType.QrLogin &&
                        x.Status == InterventionStatus.Opened && x.TokenConsumedAt == null &&
                        x.SecureEntryHash == tokenHash)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.TokenConsumedAt, now)
                .SetProperty(x => x.Status, InterventionStatus.Completed), cancellationToken);

        if (affected != 1)
            return BadRequest(new { message = "二维码授权令牌无效、已使用或已过期。" });

        // ExecuteUpdate 不同步 DbContext 已追踪的实体；读取真实落库状态再返回。
        await db.Entry(intervention).ReloadAsync(cancellationToken);
        await TryResumeExecutionAsync(intervention.ExecutionId, subjectId, cancellationToken);
        return Ok(ToDto(intervention));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<HumanInterventionDto>> Complete(Guid id, CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId))
            return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });

        var intervention = await GetOwnedInterventionAsync(id, subjectId, cancellationToken);
        if (intervention is null) return NotFound();
        intervention.Complete();
        await db.SaveChangesAsync(cancellationToken);
        if (intervention.Status == InterventionStatus.Completed)
            await TryResumeExecutionAsync(intervention.ExecutionId, subjectId, cancellationToken);
        return Ok(ToDto(intervention));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<HumanInterventionDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId))
            return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });

        var intervention = await GetOwnedInterventionAsync(id, subjectId, cancellationToken);
        if (intervention is null) return NotFound();
        intervention.Cancel();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(intervention));
    }

    private async Task TryResumeExecutionAsync(Guid executionId, Guid subjectId, CancellationToken ct)
    {
        var execution = await (from current in db.Executions
                               join item in db.TaskItems on current.TaskItemId equals item.Id
                               join task in db.Tasks on item.TaskId equals task.Id
                               where current.Id == executionId && task.SubjectId == subjectId
                               select new { Execution = current, Item = item, Task = task })
            .SingleOrDefaultAsync(ct);
        if (execution is null || execution.Execution.Status != ExecutionStatus.WaitingForHuman || !execution.Execution.NodeId.HasValue ||
            !await db.Workflows.AsNoTracking().AnyAsync(x => x.Id == execution.Task.WorkflowId && x.Status == WorkflowStatus.Published, ct))
            return;

        if (!connections.TryGet(execution.Execution.NodeId.Value, out var connectionId) || string.IsNullOrWhiteSpace(connectionId))
            return;

        // NodeAgent 仍在线时只恢复原执行上下文，不重新创建 Execution，确保浏览器 Session 保持不变。
        execution.Execution.SetStatus(ExecutionStatus.Running);
        execution.Item.Start();
        execution.Task.SetStatus(DomainTaskStatus.Running);
        await db.SaveChangesAsync(ct);
        await hub.Clients.Client(connectionId).ResumeAsync(executionId);
    }

    private async Task<Execution?> GetOwnedExecutionAsync(Guid executionId, Guid subjectId, CancellationToken ct)
    {
        return await (from execution in db.Executions
                      join item in db.TaskItems on execution.TaskItemId equals item.Id
                      join task in db.Tasks on item.TaskId equals task.Id
                      where execution.Id == executionId && task.SubjectId == subjectId
                      select execution).SingleOrDefaultAsync(ct);
    }

    private async Task<HumanIntervention?> GetOwnedInterventionAsync(Guid id, Guid subjectId, CancellationToken ct) =>
        await db.HumanInterventions.SingleOrDefaultAsync(x => x.Id == id && x.SubjectId == subjectId, ct);

    private static HumanInterventionDto ToDto(HumanIntervention x, string? qrToken = null) =>
        new(x.Id, x.ExecutionId, x.Type.ToString(), x.Status.ToString(), x.Title, x.ExpiresAt, qrToken);

    private static string CreateQrToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "-").Replace("/", "_").TrimEnd('=');

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
