using AgentRPA.Api.Security;
using AgentRPA.Application.Execution;
using AgentRPA.Domain.Execution;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>执行证据查询 API。仅允许任务所属用户读取自己的执行日志和产物。</summary>
[ApiController, Route("api/executions"), Authorize]
public sealed class ExecutionEvidenceController(AgentRpaDbContext db, IArtifactStorage artifactStorage) : ControllerBase
{
    /// <summary>单次执行的派发、步骤及终态事件，并附带当前租约快照。</summary>
    [HttpGet("{executionId:guid}/timeline")]
    public async Task<IActionResult> Timeline(Guid executionId, CancellationToken ct = default)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized();
        var execution = await db.Executions.AsNoTracking().Where(x => x.Id == executionId)
            .Join(db.TaskItems, x => x.TaskItemId, x => x.Id, (x, item) => new { Execution = x, item.TaskId })
            .Join(db.Tasks, x => x.TaskId, x => x.Id, (x, task) => new { x.Execution, task.SubjectId })
            .SingleOrDefaultAsync(ct);
        if (execution is null || execution.SubjectId != subjectId) return NotFound();

        var logs = await db.ExecutionLogs.AsNoTracking().Where(x => x.ExecutionId == executionId)
            .OrderBy(x => x.Sequence).Select(x => new { x.Sequence, x.Level, x.EventType, x.StepId, x.Message, x.Sensitive, x.CreatedAt })
            .ToListAsync(ct);
        var leases = await db.NodeLeases.AsNoTracking().Where(x => x.ExecutionId == executionId).ToListAsync(ct);
        var lease = leases.OrderBy(x => x.Released).ThenByDescending(x => x.ExpiresAt).FirstOrDefault();
        return Ok(new
        {
            executionId,
            status = execution.Execution.Status.ToString(),
            execution.Execution.NodeId,
            execution.Execution.WorkerSlotId,
            execution.Execution.Error,
            execution.Execution.CreatedAt,
            lease = lease is null ? null : new { lease.NodeId, lease.WorkerSlotId, lease.Released, lease.ExpiresAt, lease.LastHeartbeatAt },
            events = logs.Select(x => new { x.Sequence, level = x.Level.ToString(), eventType = x.EventType.ToString(), x.StepId,
                message = x.Sensitive ? "敏感信息已隐藏" : x.Message, x.CreatedAt })
        });
    }

    /// <summary>按序查看 Step 开始与完成事件；未完成的写入步骤必须先在外部系统人工核验。</summary>
    [HttpGet("{executionId:guid}/checkpoints")]
    public async Task<IActionResult> Checkpoints(Guid executionId, CancellationToken ct = default)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized();
        var ownsExecution = await db.Executions.AsNoTracking().Where(x => x.Id == executionId)
            .Join(db.TaskItems, x => x.TaskItemId, x => x.Id, (_, item) => item)
            .Join(db.Tasks, x => x.TaskId, x => x.Id, (_, task) => task.SubjectId == subjectId)
            .SingleOrDefaultAsync(ct);
        if (ownsExecution != true) return NotFound();
        return Ok(await db.ExecutionLogs.AsNoTracking().Where(x => x.ExecutionId == executionId &&
                (x.EventType == ExecutionLogEventType.StepStarted || x.EventType == ExecutionLogEventType.StepCompleted))
            .OrderBy(x => x.Sequence).Select(x => new { x.StepId, x.Sequence, EventType = x.EventType.ToString(), x.MetadataJson, x.CreatedAt })
            .ToListAsync(ct));
    }

    [HttpGet("{executionId:guid}/logs")]
    public async Task<IActionResult> Logs(Guid executionId, [FromQuery] long afterSequence = -1, [FromQuery] int limit = 200, CancellationToken ct = default)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        limit = Math.Clamp(limit, 1, 1000);
        var ownsExecution = await db.Executions.Where(x => x.Id == executionId).Join(db.TaskItems, x => x.TaskItemId, x => x.Id, (_, item) => item).Join(db.Tasks, x => x.TaskId, x => x.Id, (_, task) => task.SubjectId == subjectId).SingleOrDefaultAsync(ct);
        if (ownsExecution != true) return NotFound();
        var logs = await db.ExecutionLogs.AsNoTracking().Where(x => x.ExecutionId == executionId && x.Sequence > afterSequence).OrderBy(x => x.Sequence).Take(limit).Select(x => new { x.Id, x.Sequence, Level = x.Level.ToString(), EventType = x.EventType.ToString(), x.StepId, x.Message, x.MetadataJson, x.Sensitive, x.CreatedAt }).ToListAsync(ct);
        return Ok(logs);
    }

    [HttpGet("{executionId:guid}/artifacts")]
    public async Task<IActionResult> Artifacts(Guid executionId, CancellationToken ct = default)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        var ownsExecution = await db.Executions.AsNoTracking().Where(x => x.Id == executionId).Join(db.TaskItems, x => x.TaskItemId, x => x.Id, (_, item) => item).Join(db.Tasks, x => x.TaskId, x => x.Id, (_, task) => task.SubjectId == subjectId).SingleOrDefaultAsync(ct);
        if (ownsExecution != true) return NotFound();
        var artifacts = await db.ExecutionArtifacts.AsNoTracking().Where(x => x.ExecutionId == executionId).OrderBy(x => x.Id).Select(x => new { x.Id, x.TaskItemId, x.ArtifactType, x.FileName, x.ContentType, x.Size, x.Sha256, x.ExpiresAt, x.CreatedAt }).ToListAsync(ct);
        return Ok(artifacts);
    }

    [HttpGet("{executionId:guid}/artifacts/{artifactId:guid}/content")]
    public async Task<IActionResult> ArtifactContent(Guid executionId, Guid artifactId, CancellationToken ct = default)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        var artifact = await db.ExecutionArtifacts.AsNoTracking().Where(x => x.Id == artifactId && x.ExecutionId == executionId).Join(db.Executions, x => x.ExecutionId, x => x.Id, (x, execution) => new { Artifact = x, execution.TaskItemId }).Join(db.TaskItems, x => x.TaskItemId, x => x.Id, (x, item) => new { x.Artifact, item.TaskId }).Join(db.Tasks, x => x.TaskId, x => x.Id, (x, task) => new { x.Artifact, task.SubjectId }).SingleOrDefaultAsync(ct);
        if (artifact is null || artifact.SubjectId != subjectId) return NotFound();
        if (artifact.Artifact.ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow) return NotFound(new { message = "执行产物已过期。" });
        if (!await artifactStorage.ExistsAsync(artifact.Artifact.StorageKey, ct)) return NotFound(new { message = "执行产物文件不存在。" });
        var stream = await artifactStorage.OpenReadAsync(artifact.Artifact.StorageKey, ct);
        return File(stream, artifact.Artifact.ContentType ?? "application/octet-stream", artifact.Artifact.FileName, enableRangeProcessing: true);
    }
}
