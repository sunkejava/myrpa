using AgentRPA.Api.Security;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>LLM Token 用量统计接口，仅允许当前用户读取自己的统计数据。</summary>
[ApiController, Route("api/llm-usage"), Authorize]
public sealed class LlmUsageController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? taskId = null, [FromQuery] Guid? taskItemId = null, [FromQuery] int take = 50, [FromQuery] Guid? afterId = null, CancellationToken ct = default)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized();
        take = Math.Clamp(take, 1, 200);
        var query = db.LlmUsageRecords.AsNoTracking().Where(x => x.SubjectId == subjectId);
        if (taskId.HasValue) query = query.Where(x => x.TaskId == taskId.Value);
        if (taskItemId.HasValue) query = query.Where(x => x.TaskItemId == taskItemId.Value);
        if (afterId.HasValue) query = query.Where(x => x.Id.CompareTo(afterId.Value) > 0);
        var items = await query.OrderBy(x => x.Id).Take(take).Select(x => new { x.Id, x.TaskId, x.TaskItemId, x.ProviderId, x.Model, x.InputTokens, x.OutputTokens, TotalTokens = x.InputTokens + x.OutputTokens, x.OccurredAt }).ToListAsync(ct);
        return Ok(new { items, nextAfterId = items.Count == take ? items[^1].Id : (Guid?)null });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] Guid? taskId = null, [FromQuery] Guid? taskItemId = null, CancellationToken ct = default)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized();
        var query = db.LlmUsageRecords.AsNoTracking().Where(x => x.SubjectId == subjectId);
        if (taskId.HasValue) query = query.Where(x => x.TaskId == taskId.Value);
        if (taskItemId.HasValue) query = query.Where(x => x.TaskItemId == taskItemId.Value);
        var summary = await query.GroupBy(_ => 1).Select(g => new { Calls = g.Count(), InputTokens = g.Sum(x => x.InputTokens), OutputTokens = g.Sum(x => x.OutputTokens), TotalTokens = g.Sum(x => x.InputTokens + x.OutputTokens) }).SingleOrDefaultAsync(ct);
        return Ok(summary ?? new { Calls = 0, InputTokens = 0, OutputTokens = 0, TotalTokens = 0 });
    }
}
