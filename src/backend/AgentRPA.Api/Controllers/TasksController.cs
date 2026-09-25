using AgentRPA.Api.Security;
using AgentRPA.Api.Hubs;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Application.Batch;
using AgentRPA.Application.Permission;
using AgentRPA.Application.Workflow;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using DomainTaskStatus = AgentRPA.Domain.Tasks.TaskStatus;

namespace AgentRPA.Api.Controllers;

/// <summary>Task API。创建/导入/入队/重试均在服务端再次校验 Workflow 参数 Schema。</summary>
[ApiController, Route("api/tasks"), Authorize]
public sealed class TasksController(AgentRpaDbContext db, ISpreadsheetImportService spreadsheetImport, PermissionService permissionService, WorkflowPermissionPreflight stepPermissions, WorkflowParameterSchemaValidator parameterValidator,
    NodeAgentConnectionRegistry connections, IHubContext<NodeAgentHub, INodeAgentClient> hub, ILogger<TasksController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        var rows = await db.Tasks.AsNoTracking().Where(x => x.SubjectId == subjectId).OrderByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id, x.Name, x.WorkflowId, x.WorkflowVersion, x.MaxRetries,
                Status = x.Status.ToString(),
                Total = db.TaskItems.Count(item => item.TaskId == x.Id),
                Succeeded = db.TaskItems.Count(item => item.TaskId == x.Id && item.Status == TaskItemStatus.Succeeded),
                Failed = db.TaskItems.Count(item => item.TaskId == x.Id && item.Status == TaskItemStatus.Failed)
            }).ToListAsync(ct);
        var taskIds = rows.Select(x => x.Id).ToArray();
        var approvals = await db.TaskApprovals.AsNoTracking().Where(x => taskIds.Contains(x.TaskId))
            .Select(x => new { x.TaskId, x.Status }).ToListAsync(ct);
        var statuses = approvals.ToDictionary(x => x.TaskId, x => x.Status.ToString());
        return Ok(rows.Select(x => new { x.Id, x.Name, x.WorkflowId, x.WorkflowVersion, x.MaxRetries, x.Status,
            x.Total, x.Succeeded, x.Failed, ApprovalStatus = statuses.GetValueOrDefault(x.Id) }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        var task = await db.Tasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SubjectId == subjectId, ct); if (task is null) return NotFound();
        var items = await db.TaskItems.AsNoTracking().Where(x => x.TaskId == id).OrderBy(x => x.Sequence).Select(x => new { x.Id, x.Sequence, x.Status, x.RetryCount, x.ResultJson }).ToListAsync(ct);
        var itemIds = items.Select(x => x.Id).ToArray();
        var executions = await db.Executions.AsNoTracking().Where(x => itemIds.Contains(x.TaskItemId))
            .Select(x => new { x.Id, x.TaskItemId, x.DispatchKey, Status = x.Status.ToString(), x.Error }).ToListAsync(ct);
        var approval = await db.TaskApprovals.AsNoTracking().Where(x => x.TaskId == id).Select(x => (TaskApprovalStatus?)x.Status).SingleOrDefaultAsync(ct);
        return Ok(new { task.Id, task.Name, task.WorkflowId, task.WorkflowVersion, task.MaxRetries, Status = task.Status.ToString(), ApprovalStatus = approval?.ToString(),
            Items = items.Select(x => new { x.Id, x.Sequence, Status = x.Status.ToString(), x.RetryCount, x.ResultJson,
                Executions = executions.Where(e => e.TaskItemId == x.Id)
                    .OrderByDescending(e => int.TryParse(e.DispatchKey[(e.DispatchKey.LastIndexOf(':') + 1)..], out var retry) ? retry : -1)
                    .Select(e => new { e.Id, e.TaskItemId, e.Status, e.Error }).ToArray() }) });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTaskRequest request, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        var scope = await ResolveWorkflowScopeAsync(request.WorkflowId, request.WorkflowVersion, ct); if (scope is null) return UnprocessableEntity(new { message = "Workflow 或已发布版本不存在。" });
        var permission = await permissionService.CheckAsync(subjectId, scope.Value.CityId, scope.Value.SystemId, scope.Value.FunctionId, scope.Value.Action, ct); if (!permission.Allowed) return StatusCode(StatusCodes.Status403Forbidden, permission);
        var definition = await GetDefinitionAsync(request.WorkflowId, request.WorkflowVersion, ct); if (definition is null) return UnprocessableEntity(new { message = "Workflow 定义不存在。" });
        var stepsAllowed = await stepPermissions.CheckAsync(subjectId, scope.Value.CityId, scope.Value.SystemId, scope.Value.FunctionId, definition.Value.GetRawText(), ct);
        if (!stepsAllowed.Allowed) return StatusCode(StatusCodes.Status403Forbidden, stepsAllowed);
        var errors = request.Items is null ? [] : request.Items.SelectMany(item => ValidateInput(definition.Value, item)).ToArray();
        if (errors.Length > 0) return BadRequest(new { message = "任务参数校验失败。", errors });
        var task = new RpaTask(request.WorkflowId, request.WorkflowVersion, request.Name, request.MaxRetries, subjectId);
        foreach (var item in request.Items ?? []) task.AddItem(item);
        db.Tasks.Add(task);
        TaskApprovalGate.AddPending(db, task, subjectId, definition.Value.GetRawText());
        await db.SaveChangesAsync(ct);
        return Created($"api/tasks/{task.Id}", new { task.Id, approvalRequired = WorkflowApprovalPolicy.RequiresApproval(definition.Value.GetRawText()) });
    }

    [HttpPost("{id:guid}/import"), RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Import(Guid id, IFormFile file, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        if (file.Length == 0) return BadRequest(new { message = "上传文件为空。" });
        var task = await db.Tasks.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id && x.SubjectId == subjectId, ct); if (task is null) return NotFound();
        if (task.Status == DomainTaskStatus.Cancelled) return Conflict(new { message = "任务已取消，不能继续导入。" });
        var definition = await GetDefinitionAsync(task.WorkflowId, task.WorkflowVersion, ct); if (definition is null) return UnprocessableEntity(new { message = "Workflow 定义不存在。" });
        var scope = await ResolveWorkflowScopeAsync(task.WorkflowId, task.WorkflowVersion, ct); if (scope is null) return UnprocessableEntity(new { message = "Workflow 已失效。" });
        var stepsAllowed = await stepPermissions.CheckAsync(subjectId, scope.Value.CityId, scope.Value.SystemId, scope.Value.FunctionId, definition.Value.GetRawText(), ct);
        if (!stepsAllowed.Allowed) return StatusCode(StatusCodes.Status403Forbidden, stepsAllowed);
        await using var stream = file.OpenReadStream(); var rows = await spreadsheetImport.ReadAsync(stream, file.FileName, ct);
        var validationErrors = new List<string>();
        foreach (var row in rows) { var json = JsonSerializer.Serialize(row); validationErrors.AddRange(ValidateInput(definition.Value, json)); }
        if (validationErrors.Count > 0) return BadRequest(new { message = "导入数据未通过 Workflow 参数校验。", errors = validationErrors.Take(100) });
        foreach (var row in rows) task.AddItem(JsonSerializer.Serialize(row));
        var approval = await TaskApprovalGate.GetStatusAsync(db, task.Id, definition.Value.GetRawText(), ct);
        if (rows.Count > 0 && approval is null) task.Queue();
        await db.SaveChangesAsync(ct); return Ok(new { imported = rows.Count, taskId = task.Id, approvalStatus = approval?.ToString() });
    }

    [HttpPost("{id:guid}/queue")]
    public async Task<IActionResult> Queue(Guid id, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == id && x.SubjectId == subjectId, ct); if (task is null) return NotFound();
        if (task.Status == DomainTaskStatus.Cancelled) return Conflict(new { message = "任务已取消，不能重新入队。" });
        var scope = await ResolveWorkflowScopeAsync(task.WorkflowId, task.WorkflowVersion, ct); if (scope is null) return UnprocessableEntity(new { message = "Workflow 已失效。" });
        var permission = await permissionService.CheckAsync(subjectId, scope.Value.CityId, scope.Value.SystemId, scope.Value.FunctionId, scope.Value.Action, ct); if (!permission.Allowed) return StatusCode(StatusCodes.Status403Forbidden, permission);
        var definition = await GetDefinitionAsync(task.WorkflowId, task.WorkflowVersion, ct); if (definition is null) return UnprocessableEntity(new { message = "Workflow 定义不存在。" });
        var approval = await TaskApprovalGate.GetStatusAsync(db, id, definition.Value.GetRawText(), ct);
        if (approval == TaskApprovalStatus.Pending) return StatusCode(StatusCodes.Status428PreconditionRequired, new { message = "任务等待管理员审批。", approvalStatus = "Pending" });
        if (approval == TaskApprovalStatus.Rejected) return StatusCode(StatusCodes.Status403Forbidden, new { message = "任务审批已拒绝。", approvalStatus = "Rejected" });
        var stepsAllowed = await stepPermissions.CheckAsync(subjectId, scope.Value.CityId, scope.Value.SystemId, scope.Value.FunctionId, definition.Value.GetRawText(), ct);
        if (!stepsAllowed.Allowed) return StatusCode(StatusCodes.Status403Forbidden, stepsAllowed);
        foreach (var item in await db.TaskItems.AsNoTracking().Where(x => x.TaskId == id && x.Status == TaskItemStatus.Pending).ToListAsync(ct)) { var errors = ValidateInput(definition.Value, item.InputJson); if (errors.Count > 0) return BadRequest(new { message = $"TaskItem {item.Sequence} 参数校验失败。", errors }); }
        task.Queue(); await db.SaveChangesAsync(ct); return Ok();
    }

    [HttpPost("{id:guid}/retry-failed")]
    public async Task<IActionResult> RetryFailed(Guid id, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        var task = await db.Tasks.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id && x.SubjectId == subjectId, ct); if (task is null) return NotFound();
        if (task.Status == DomainTaskStatus.Cancelled) return Conflict(new { message = "任务已取消，不能重试。" });
        var scope = await ResolveWorkflowScopeAsync(task.WorkflowId, task.WorkflowVersion, ct); if (scope is null) return UnprocessableEntity(new { message = "Workflow 已失效。" });
        var permission = await permissionService.CheckAsync(subjectId, scope.Value.CityId, scope.Value.SystemId, scope.Value.FunctionId, scope.Value.Action, ct); if (!permission.Allowed) return StatusCode(StatusCodes.Status403Forbidden, permission);
        var definition = await GetDefinitionAsync(task.WorkflowId, task.WorkflowVersion, ct); if (definition is null) return UnprocessableEntity(new { message = "Workflow 定义不存在。" });
        var approval = await TaskApprovalGate.GetStatusAsync(db, id, definition.Value.GetRawText(), ct);
        if (approval == TaskApprovalStatus.Pending) return StatusCode(StatusCodes.Status428PreconditionRequired, new { message = "任务等待管理员审批。", approvalStatus = "Pending" });
        if (approval == TaskApprovalStatus.Rejected) return StatusCode(StatusCodes.Status403Forbidden, new { message = "任务审批已拒绝。", approvalStatus = "Rejected" });
        var stepsAllowed = await stepPermissions.CheckAsync(subjectId, scope.Value.CityId, scope.Value.SystemId, scope.Value.FunctionId, definition.Value.GetRawText(), ct);
        if (!stepsAllowed.Allowed) return StatusCode(StatusCodes.Status403Forbidden, stepsAllowed);
        if (task.Items.Any(item => item.CanRetry(task.MaxRetries)) && !WorkflowRetrySafety.IsSafeToRetry(definition.Value.GetRawText()))
            return Conflict(new { message = "Workflow 含提交、上传、下载、人工操作或高风险步骤；必须先核验外部系统状态，禁止盲目重试。" });
        var count = 0; foreach (var item in task.Items) if (item.CanRetry(task.MaxRetries)) { var errors = ValidateInput(definition.Value, item.InputJson); if (errors.Count > 0) return BadRequest(new { message = $"TaskItem {item.Sequence} 参数校验失败。", errors }); item.Retry(); count++; }
        if (count > 0) task.Queue(); await db.SaveChangesAsync(ct); return Ok(new { retried = count });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (!CurrentUser.TryGetSubjectId(User, out var subjectId)) return Unauthorized(new { message = "JWT 缺少有效的用户主体。" });
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == id && x.SubjectId == subjectId, ct);
        if (task is null) return NotFound();
        if (task.Status is DomainTaskStatus.Succeeded or DomainTaskStatus.Failed)
            return Conflict(new { message = "任务已结束，不能取消。" });
        var active = await (from execution in db.Executions.AsNoTracking()
            join item in db.TaskItems.AsNoTracking() on execution.TaskItemId equals item.Id
            where item.TaskId == id && execution.NodeId != null &&
                (execution.Status == ExecutionStatus.Dispatched || execution.Status == ExecutionStatus.Running ||
                 execution.Status == ExecutionStatus.Paused || execution.Status == ExecutionStatus.WaitingForHuman)
            select new { execution.Id, execution.NodeId }).ToListAsync(ct);
        if (task.Status != DomainTaskStatus.Cancelled)
        {
            task.SetStatus(DomainTaskStatus.Cancelled);
            await db.SaveChangesAsync(ct);
        }
        var signaled = 0;
        foreach (var execution in active)
        {
            if (!connections.TryGet(execution.NodeId!.Value, out var connectionId) || connectionId is null) continue;
            try { await hub.Clients.Client(connectionId).CancelAsync(execution.Id); signaled++; }
            catch (Exception ex) { logger.LogWarning(ex, "任务 {TaskId} 取消通知 Execution {ExecutionId} 失败", id, execution.Id); }
        }
        return Ok(new { task.Id, status = task.Status.ToString(), activeExecutions = active.Count, signaled });
    }

    private async Task<JsonElement?> GetDefinitionAsync(Guid workflowId, int version, CancellationToken ct)
    {
        var json = await db.WorkflowVersions.AsNoTracking().Where(x => x.WorkflowId == workflowId && x.Version == version && x.Published).Select(x => x.DefinitionJson).SingleOrDefaultAsync(ct);
        if (json is null) return null;
        try { using var doc = JsonDocument.Parse(json); return doc.RootElement.Clone(); } catch (JsonException) { return null; }
    }

    private IReadOnlyList<string> ValidateInput(JsonElement definition, string inputJson)
    {
        try { using var doc = JsonDocument.Parse(inputJson); if (doc.RootElement.ValueKind != JsonValueKind.Object) return ["TaskItem 输入必须是 JSON Object。"]; var values = doc.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => (object?)x.Value.Clone(), StringComparer.OrdinalIgnoreCase); return parameterValidator.ValidateParameters(definition, values); }
        catch (JsonException) { return ["TaskItem 输入不是有效 JSON。"]; }
    }

    private async Task<(Guid CityId, Guid SystemId, Guid FunctionId, string Action)?> ResolveWorkflowScopeAsync(Guid workflowId, int version, CancellationToken ct)
    {
        var row = await db.WorkflowVersions.AsNoTracking().Where(x => x.WorkflowId == workflowId && x.Version == version && x.Published).Join(db.Workflows.AsNoTracking().Where(w => w.Status == AgentRPA.Domain.Workflow.WorkflowStatus.Published), v => v.WorkflowId, w => w.Id, (v, w) => new { w.BusinessFunctionId }).SingleOrDefaultAsync(ct); if (row is null) return null;
        var scope = await db.BusinessFunctions.AsNoTracking().Where(f => f.Id == row.BusinessFunctionId).Join(db.BusinessSystems.AsNoTracking(), f => f.SystemId, s => s.Id, (f, s) => new { FunctionId = f.Id, SystemId = s.Id, s.CityId }).SingleOrDefaultAsync(ct);
        return scope is null ? null : (scope.CityId, scope.SystemId, scope.FunctionId, "Execute");
    }
}

public sealed record CreateTaskRequest(Guid WorkflowId, int WorkflowVersion, string Name, IReadOnlyCollection<string>? Items, int MaxRetries = 3);
