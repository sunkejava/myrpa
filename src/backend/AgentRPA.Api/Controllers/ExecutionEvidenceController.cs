using AgentRPA.Api.Security;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>执行证据查询 API。仅允许任务所属用户读取自己的执行日志和产物元数据。</summary>
[ApiController, Route("api/executions"), Authorize]
public sealed class ExecutionEvidenceController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet("{executionId:guid}/logs")]
    public async Task<IActionResult> Logs(Guid executionId, [FromQuery] long afterSequence = -1, [FromQuery] int limit = 200, CancellationToken ct = default)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId))
            return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });

        limit = Math.Clamp(limit, 1, 1000);
        var ownsExecution = await db.Executions
            .AsNoTracking()
            .Where(x => x.Id == executionId)
            .Join(db.TaskItems, x => x.TaskItemId, x => x.Id, (_, item) => item)
            .Join(db.Tasks, x => x.TaskId, x => x.Id, (_, task) => task.SubjectId == subjectId)
            .SingleOrDefaultAsync(ct);

        if (ownsExecution != true) return NotFound();

        var logs = await db.ExecutionLogs
            .AsNoTracking()
            .Where(x => x.ExecutionId == executionId && x.Sequence > afterSequence)
            .OrderBy(x => x.Sequence)
            .Take(limit)
            .Select(x => new
            {
                x.Id,
                x.Sequence,
                Level = x.Level.ToString(),
                EventType = x.EventType.ToString(),
                x.StepId,
                x.Message,
                x.MetadataJson,
                x.Sensitive,
                x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(logs);
    }

    [HttpGet("{executionId:guid}/artifacts")]
    public async Task<IActionResult> Artifacts(Guid executionId, CancellationToken ct = default)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId))
            return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });

        var ownsExecution = await db.Executions
            .AsNoTracking()
            .Where(x => x.Id == executionId)
            .Join(db.TaskItems, x => x.TaskItemId, x => x.Id, (_, item) => item)
            .Join(db.Tasks, x => x.TaskId, x => x.Id, (_, task) => task.SubjectId == subjectId)
            .SingleOrDefaultAsync(ct);

        if (ownsExecution != true) return NotFound();

        var artifacts = await db.ExecutionArtifacts
            .AsNoTracking()
            .Where(x => x.ExecutionId == executionId)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.TaskItemId,
                x.ArtifactType,
                x.FileName,
                x.ContentType,
                x.Size,
                x.Sha256,
                x.ExpiresAt,
                x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(artifacts);
    }
}
