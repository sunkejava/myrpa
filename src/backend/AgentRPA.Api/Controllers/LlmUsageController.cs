using AgentRPA.Api.Security;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>LLM 用量统计接口，仅允许当前用户读取自己的统计数据。</summary>
[ApiController, Route("api/llm-usage"), Authorize]
public sealed class LlmUsageController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 50, [FromQuery] Guid? beforeId = null, CancellationToken ct = default)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized();
        take = Math.Clamp(take, 1, 200);

        var query = db.LlmUsageRecords.AsNoTracking()
            .Where(x => x.SubjectId == subjectId);
        if (beforeId.HasValue) query = query.Where(x => x.Id.CompareTo(beforeId.Value) < 0);

        var items = await query
            .OrderByDescending(x => x.Id)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.TaskId,
                x.TaskItemId,
                x.ProviderId,
                x.Model,
                x.InputTokens,
                x.OutputTokens,
                TotalTokens = x.InputTokens + x.OutputTokens,
                x.OccurredAt
            })
            .ToListAsync(ct);

        return Ok(new { items, nextBeforeId = items.Count == take ? items[^1].Id : (Guid?)null });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized();
        var summary = await db.LlmUsageRecords.AsNoTracking()
            .Where(x => x.SubjectId == subjectId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Calls = g.Count(),
                InputTokens = g.Sum(x => x.InputTokens),
                OutputTokens = g.Sum(x => x.OutputTokens),
                TotalTokens = g.Sum(x => x.InputTokens + x.OutputTokens)
            })
            .SingleOrDefaultAsync(ct);
        return Ok(summary ?? new { Calls = 0, InputTokens = 0, OutputTokens = 0, TotalTokens = 0 });
    }
}
