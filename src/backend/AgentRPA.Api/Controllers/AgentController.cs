using AgentRPA.Application.Agent;
using Microsoft.AspNetCore.Mvc;

namespace AgentRPA.Api.Controllers;

/// <summary>自然语言 Agent 规划接口。这里只生成计划，不直接执行外部业务系统。</summary>
[ApiController, Route("api/agent")]
public sealed class AgentController(AgentPlanningService planner) : ControllerBase
{
    [HttpPost("plan")]
    public async Task<IActionResult> Plan(AgentPlanRequest request, CancellationToken ct)
    {
        var result = await planner.PlanAsync(request.Instruction, ct);
        return result.Success ? Ok(result) : UnprocessableEntity(result);
    }
}

public sealed record AgentPlanRequest(string Instruction);
