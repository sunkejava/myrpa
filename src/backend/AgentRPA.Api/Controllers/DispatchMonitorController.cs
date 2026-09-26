using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>管理员调度概览，状态汇总在数据库聚合，最近记录限制数量避免全表加载。</summary>
[ApiController, Route("api/dispatch-monitor"), Authorize(Roles = "Admin")]
public sealed class DispatchMonitorController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int limit = 30, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var taskCounts = await db.Tasks.AsNoTracking().GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(ct);
        var executionCounts = await db.Executions.AsNoTracking().GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(ct);
        var nodeCounts = await db.ExecutionNodes.AsNoTracking().GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(ct);
        var slotCounts = await db.WorkerSlots.AsNoTracking().GroupBy(x => new { x.Enabled, Busy = x.ExecutionId != null })
            .Select(x => new { x.Key.Enabled, x.Key.Busy, Count = x.Count() }).ToListAsync(ct);
        // SQLite 无法在 LINQ 中对 DateTimeOffset 排序；UTC 时间在数据库中按 ISO 文本排序。
        var recentExecutions = await db.Executions.FromSqlInterpolated(
            $"SELECT * FROM \"Executions\" ORDER BY \"CreatedAt\" DESC LIMIT {limit}")
            .AsNoTracking().ToListAsync(ct);
        var recentIds = recentExecutions.Select(x => x.Id).ToArray();
        var details = await db.Executions.AsNoTracking().Where(x => recentIds.Contains(x.Id))
            .Join(db.TaskItems, execution => execution.TaskItemId, item => item.Id,
                (execution, item) => new { Execution = execution, item.TaskId, item.Sequence })
            .Join(db.Tasks, row => row.TaskId, task => task.Id,
                (row, task) => new { row.Execution, row.TaskId, row.Sequence, TaskName = task.Name })
            .Select(x => new { x.Execution.Id, x.TaskId, x.TaskName, x.Sequence, x.Execution.NodeId,
                x.Execution.WorkerSlotId, x.Execution.Status, x.Execution.Error, x.Execution.CreatedAt })
            .ToListAsync(ct);
        var recent = details.OrderByDescending(x => x.CreatedAt).ToList();
        var nodeIds = recent.Where(x => x.NodeId.HasValue).Select(x => x.NodeId!.Value).Distinct().ToArray();
        var nodeNames = await db.ExecutionNodes.AsNoTracking().Where(x => nodeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return Ok(new
        {
            taskCounts = taskCounts.Select(x => new { status = x.Status.ToString(), x.Count }),
            executionCounts = executionCounts.Select(x => new { status = x.Status.ToString(), x.Count }),
            nodeCounts = nodeCounts.Select(x => new { status = x.Status.ToString(), x.Count }),
            slotCounts,
            recent = recent.Select(x => new { x.Id, x.TaskId, x.TaskName, itemSequence = x.Sequence,
                x.NodeId, nodeName = x.NodeId.HasValue && nodeNames.TryGetValue(x.NodeId.Value, out var name) ? name : null,
                x.WorkerSlotId, status = x.Status.ToString(), x.Error, x.CreatedAt })
        });
    }
}
