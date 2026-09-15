using AgentRPA.Api.Security;
using AgentRPA.Application.Batch;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgentRPA.Api.Controllers;

[ApiController, Route("api/tasks"), Authorize]
public sealed class TasksController(AgentRpaDbContext db, ISpreadsheetImportService spreadsheetImport) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        return Ok(await db.Tasks.AsNoTracking().Where(x => x.SubjectId == subjectId).OrderByDescending(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.WorkflowId, x.WorkflowVersion, x.MaxRetries, Status = x.Status.ToString() }).ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        var task = await db.Tasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SubjectId == subjectId, ct); if (task is null) return NotFound();
        var items = await db.TaskItems.AsNoTracking().Where(x => x.TaskId == id).OrderBy(x => x.Sequence).Select(x => new { x.Id, x.Sequence, x.Status, x.RetryCount, x.ResultJson }).ToListAsync(ct);
        return Ok(new { task.Id, task.Name, task.WorkflowId, task.WorkflowVersion, task.MaxRetries, Status = task.Status.ToString(), Items = items });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTaskRequest request, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        var task = new RpaTask(request.WorkflowId, request.WorkflowVersion, request.Name, request.MaxRetries, subjectId);
        foreach (var item in request.Items ?? []) task.AddItem(item);
        db.Tasks.Add(task); await db.SaveChangesAsync(ct); return Created($"api/tasks/{task.Id}", new { task.Id });
    }

    /// <summary>从 CSV/XLSX 导入批量 TaskItem。每一行独立成为可重试任务项。</summary>
    [HttpPost("{id:guid}/import")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Import(Guid id, IFormFile file, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        if (file.Length == 0) return BadRequest(new { message = "上传文件为空。" });
        var task = await db.Tasks.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id && x.SubjectId == subjectId, ct);
        if (task is null) return NotFound();
        await using var stream = file.OpenReadStream();
        var rows = await spreadsheetImport.ReadAsync(stream, file.FileName, ct);
        foreach (var row in rows) task.AddItem(JsonSerializer.Serialize(row));
        if (rows.Count > 0) task.Queue();
        await db.SaveChangesAsync(ct);
        return Ok(new { imported = rows.Count, taskId = task.Id });
    }

    [HttpPost("{id:guid}/queue")]
    public async Task<IActionResult> Queue(Guid id, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == id && x.SubjectId == subjectId, ct); if (task is null) return NotFound();
        task.Queue(); await db.SaveChangesAsync(ct); return Ok();
    }

    [HttpPost("{id:guid}/retry-failed")]
    public async Task<IActionResult> RetryFailed(Guid id, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        var task = await db.Tasks.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id && x.SubjectId == subjectId, ct); if (task is null) return NotFound();
        var count = 0; foreach (var item in task.Items) if (item.CanRetry(task.MaxRetries)) { item.Retry(); count++; }
        if (count > 0) task.Queue(); await db.SaveChangesAsync(ct); return Ok(new { retried = count });
    }
}

public sealed record CreateTaskRequest(Guid WorkflowId, int WorkflowVersion, string Name, IReadOnlyCollection<string>? Items, int MaxRetries = 3);
