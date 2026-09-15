namespace AgentRPA.Contracts.Tasks;

/// <summary>Agent 生成并经权限校验后的可执行任务计划。</summary>
public sealed record TaskPlanDto(
    Guid CityId,
    Guid SystemId,
    Guid FunctionId,
    string Action,
    IReadOnlyDictionary<string, object?> Parameters,
    IReadOnlyList<Guid> InputFileIds,
    string? WorkflowId,
    int? WorkflowVersion,
    string RiskLevel,
    bool RequiresConfirmation);
