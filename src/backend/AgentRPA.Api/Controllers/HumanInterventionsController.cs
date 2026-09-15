using AgentRPA.Contracts.Interventions;
using AgentRPA.Domain.HumanIntervention;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>人工介入控制面 API，供验证码、扫码、人脸和 UKey 场景暂停/恢复执行。</summary>
[ApiController]
[Route("api/human-interventions")]
public sealed class HumanInterventionsController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<HumanInterventionDto>> List([FromQuery] Guid? executionId, CancellationToken cancellationToken)
    {
        var query = db.HumanInterventions.AsNoTracking();
        if (executionId.HasValue)
            query = query.Where(x => x.ExecutionId == executionId.Value);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new HumanInterventionDto(
                x.Id,
                x.ExecutionId,
                x.Type.ToString(),
                x.Status.ToString(),
                x.Title,
                x.ExpiresAt))
            .ToListAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<HumanInterventionDto>> Create(CreateHumanInterventionRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<InterventionType>(request.Type, true, out var type))
            return BadRequest(new { message = "不支持的人工介入类型。" });

        var executionExists = await db.Executions.AnyAsync(x => x.Id == request.ExecutionId, cancellationToken);
        if (!executionExists)
            return NotFound(new { message = "Execution 不存在。" });

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
        var intervention = await db.HumanInterventions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (intervention is null) return NotFound();
        intervention.Complete();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(intervention));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<HumanInterventionDto>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var intervention = await db.HumanInterventions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (intervention is null) return NotFound();
        intervention.Cancel();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(intervention));
    }

    private static HumanInterventionDto ToDto(HumanIntervention x) =>
        new(x.Id, x.ExecutionId, x.Type.ToString(), x.Status.ToString(), x.Title, x.ExpiresAt);
}
