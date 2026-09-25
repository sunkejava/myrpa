using System.Text.Json;
using AgentRPA.Api.Security;
using AgentRPA.Application.Workflow;
using AgentRPA.Domain.Execution;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DomainTaskStatus = AgentRPA.Domain.Tasks.TaskStatus;

namespace AgentRPA.Api.Controllers;

/// <summary>高风险失败任务的人工外部核验；仅由不同于发起人的管理员作出决定。</summary>
[ApiController, Route("api/task-reconciliations"), Authorize(Roles = "Admin")]
public sealed class TaskReconciliationController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await (from execution in db.Executions.AsNoTracking()
            join item in db.TaskItems.AsNoTracking() on execution.TaskItemId equals item.Id
            join task in db.Tasks.AsNoTracking() on item.TaskId equals task.Id
            join approval in db.TaskApprovals.AsNoTracking() on task.Id equals approval.TaskId
            where execution.Status == ExecutionStatus.Failed && item.Status == TaskItemStatus.Failed &&
                task.Status == DomainTaskStatus.Failed && approval.Status == TaskApprovalStatus.Approved
            select new { execution.Id, execution.DispatchKey, itemId = item.Id, item.RetryCount,
                taskId = task.Id, task.Name, task.SubjectId, execution.Error })
            .Take(200).ToListAsync(ct);
        var pending = rows.Where(x => x.DispatchKey == $"{x.itemId:N}:{x.RetryCount}").Take(100).ToArray();
        var ids = pending.Select(x => x.Id).ToArray();
        var checkpoints = await db.ExecutionLogs.AsNoTracking().Where(x => ids.Contains(x.ExecutionId) &&
                (x.EventType == ExecutionLogEventType.StepStarted || x.EventType == ExecutionLogEventType.StepCompleted))
            .OrderBy(x => x.Sequence).Select(x => new { x.ExecutionId, x.StepId, x.Sequence, x.EventType }).ToListAsync(ct);
        return Ok(pending.Select(x => new { executionId = x.Id, x.taskId, x.Name, x.SubjectId, x.Error,
            Checkpoints = checkpoints.Where(c => c.ExecutionId == x.Id).TakeLast(12).Select(c => new { c.StepId, eventType = c.EventType.ToString() }) }));
    }

    [HttpPost("{executionId:guid}/decide")]
    public async Task<IActionResult> Decide(Guid executionId, ReconcileExecutionRequest request, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var reviewerId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.EvidenceReference) || request.EvidenceReference.Length > 256 ||
            request.EvidenceReference.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.' or ':' or '/')))
            return BadRequest(new { message = "外部核验凭据须为 1 至 256 位的编号或路径，不能包含个人信息。" });
        if (request.Decision is not ("Submitted" or "NotSubmitted"))
            return BadRequest(new { message = "decision 必须是 Submitted 或 NotSubmitted。" });

        // Serialized write transaction prevents a second reviewer from consuming the same failed item.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var execution = await db.Executions.SingleOrDefaultAsync(x => x.Id == executionId, ct);
        if (execution is null) return NotFound();
        var item = await db.TaskItems.SingleOrDefaultAsync(x => x.Id == execution.TaskItemId, ct);
        var task = item is null ? null : await db.Tasks.SingleOrDefaultAsync(x => x.Id == item.TaskId, ct);
        if (task is null || item is null) return NotFound();
        if (task.SubjectId == reviewerId) return Forbid();
        if (task.Status != DomainTaskStatus.Failed || item.Status != TaskItemStatus.Failed || execution.Status != ExecutionStatus.Failed ||
            execution.DispatchKey != $"{item.Id:N}:{item.RetryCount}")
            return Conflict(new { message = "只能核验已失败且尚未处理的执行。" });
        if (await db.Executions.AnyAsync(x => x.TaskItemId == item.Id && x.Id != executionId && x.Status != ExecutionStatus.Failed, ct))
            return Conflict(new { message = "存在其他执行记录，请先核对最新执行状态。" });
        var definition = await db.WorkflowVersions.AsNoTracking().Where(x => x.Id == execution.WorkflowVersionId)
            .Select(x => x.DefinitionJson).SingleOrDefaultAsync(ct);
        if (definition is null || !WorkflowApprovalPolicy.RequiresApproval(definition))
            return Conflict(new { message = "该接口仅处理需要审批的高风险 Workflow。" });
        if (await TaskApprovalGate.GetStatusAsync(db, task.Id, definition, ct) != TaskApprovalStatus.Approved)
            return Conflict(new { message = "任务没有有效的管理员审批。" });
        if (request.Decision == "NotSubmitted" && !item.CanRetry(task.MaxRetries))
            return Conflict(new { message = "该任务项已达到最大重试次数。" });

        var nextSequence = (await db.ExecutionLogs.Where(x => x.ExecutionId == executionId)
            .Select(x => (long?)x.Sequence).MaxAsync(ct) ?? -1) + 1;
        db.ExecutionLogs.Add(new ExecutionLog(executionId, nextSequence, ExecutionLogLevel.Warning,
            ExecutionLogEventType.ManualReconciliation, "管理员已记录外部状态核验结论。", metadataJson: JsonSerializer.Serialize(new
            {
                reviewerId, decision = request.Decision, evidenceReference = request.EvidenceReference
            })));
        if (request.Decision == "Submitted")
        {
            item.Succeed("管理员复核：外部业务状态已完成；原 Execution 仍保留失败记录。");
            var others = await db.TaskItems.AsNoTracking().Where(x => x.TaskId == task.Id && x.Id != item.Id)
                .Select(x => x.Status).ToListAsync(ct);
            task.SetStatus(others.All(x => x is TaskItemStatus.Succeeded or TaskItemStatus.Skipped)
                ? DomainTaskStatus.Succeeded : others.Any(x => x == TaskItemStatus.Failed) ? DomainTaskStatus.Failed : DomainTaskStatus.Queued);
        }
        else
        {
            item.Retry();
            task.Queue();
        }
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException) { await transaction.RollbackAsync(ct); return Conflict(new { message = "核验记录发生并发冲突，请刷新任务后重试。" }); }
        return Ok(new { executionId, item.Id, itemStatus = item.Status.ToString(), taskStatus = task.Status.ToString(), decision = request.Decision });
    }
}

public sealed record ReconcileExecutionRequest(string Decision, string EvidenceReference);
