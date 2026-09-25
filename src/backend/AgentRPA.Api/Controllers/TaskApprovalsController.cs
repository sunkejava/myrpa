using AgentRPA.Api.Security;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>管理员审核高风险任务；审批者和发起人必须是不同账户。</summary>
[ApiController, Route("api/task-approvals"), Authorize(Roles = "Admin")]
public sealed class TaskApprovalsController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TaskApprovalStatus? status, CancellationToken ct)
        => Ok(await db.TaskApprovals.AsNoTracking().Where(x => !status.HasValue || x.Status == status.Value)
            .OrderByDescending(x => x.Id).Take(200)
            .Select(x => new { x.Id, x.TaskId, x.RequesterId, x.ReviewerId, Status = x.Status.ToString(), x.Reason, x.CreatedAt, x.ReviewedAt })
            .ToListAsync(ct));

    [HttpPost("{id:guid}/decide")]
    public async Task<IActionResult> Decide(Guid id, DecideTaskApprovalRequest request, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var reviewerId)) return Unauthorized();
        var approval = await db.TaskApprovals.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (approval is null) return NotFound();
        if (approval.RequesterId == reviewerId) return StatusCode(StatusCodes.Status403Forbidden, new { message = "不得审批本人发起的任务。" });
        var task = await db.Tasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == approval.TaskId, ct);
        if (task is null || task.SubjectId != approval.RequesterId || task.Status != AgentRPA.Domain.Tasks.TaskStatus.Draft)
            return Conflict(new { message = "任务已失效或已进入执行状态。" });
        try { approval.Decide(reviewerId, request.Approved, request.Reason); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "审批已由其他管理员处理，请刷新。" }); }
        return Ok(new { approval.Id, approval.TaskId, status = approval.Status.ToString(), approval.ReviewerId, approval.Reason });
    }
}

public sealed record DecideTaskApprovalRequest(bool Approved, string? Reason);
