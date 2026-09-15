namespace AgentRPA.Contracts.Interventions;

public sealed record CreateHumanInterventionRequest(
    Guid ExecutionId,
    string Type,
    string Title,
    DateTimeOffset ExpiresAt,
    string? SecureEntry = null);

public sealed record HumanInterventionDto(
    Guid Id,
    Guid ExecutionId,
    string Type,
    string Status,
    string Title,
    DateTimeOffset ExpiresAt);
