using AgentRPA.Api.Security;
using AgentRPA.Contracts.Interventions;
using AgentRPA.Domain.HumanIntervention;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>人工介入控制面 API，供验证码、扫码、人脸和 UKey 场景暂停/恢复执行。</summary>
[ApiController]
[Route("api/human-interventions")]
[Authorize]
public sealed class HumanInterventionsController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HumanInterventionDto>>> List([FromQuery] Guid? executionId, CancellationToken cancellationToken)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId))
            return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });

        var query = from intervention in db.HumanInterventions.AsNoTracking()
                    join execution in db.Executions.AsNoTracking() on intervention.ExecutionId equals execution.Id
                    join item in db.TaskItems.AsNoTracking() on execution.TaskItemId equals item.Id
                    join task in db.Tasks.AsNoTracking() on item.TaskId equals task.Id
                    where task.SubjectId == subjectId
                    select intervention;

        if (executionId.HasValue)
            query = query.Where(x => x.ExecutionId == executionId.Value);

        // SQLite 项目避免按 DateTimeOffset 排序，防止 provider 不支持该表达式。
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

        if (request.ExpiresAt <= DateTimeOffset.UtcNow)
            return BadRequest(new { message = "人工介入过期时间必须晚于当前时间。" });

        var intervention = new HumanIntervention(request.ExecutionId, type, request.Title, request.ExpiresAt);
        if (!string.IsNullOrWhiteSpace(request.SecureEntry))
            intervention.Open(request.SecureEntry);

        db.HumanInterventions.Add(intervention);
        await db.SaveChangesAsync(cancellationToken);
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

    private async Task<Execution?> GetOwnedExecutionAsync(Guid executionId, Guid subjectId, CancellationToken ct)
    {
        return await (from execution in db.Executions
                      join item in db.TaskItems on execution.TaskItemId equals item.Id
                      join task in db.Tasks on item.TaskId equals task.Id
                      where execution.Id == executionId && task.SubjectId == subjectId
                      select execution).SingleOrDefaultAsync(ct);
    }

    private async Task<HumanIntervention?> GetOwnedInterventionAsync(Guid id, Guid subjectId, CancellationToken ct)
    {
        return await (from intervention in db.HumanInterventions
                      join execution in db.Executions on intervention.ExecutionId equals execution.Id
                      join item in db.TaskItems on execution.TaskItemId equals item.Id
                      join task in db.Tasks on item.TaskId equals task.Id
                      where intervention.Id == id && task.SubjectId == subjectId
                      select intervention).SingleOrDefaultAsync(ct);
    }

    private static HumanInterventionDto ToDto(HumanIntervention x) =>
        new(x.Id, x.ExecutionId, x.Type.ToString(), x.Status.ToString(), x.Title, x.ExpiresAt);
}
