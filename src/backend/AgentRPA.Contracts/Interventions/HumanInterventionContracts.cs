namespace AgentRPA.Contracts.Interventions;

public sealed record CreateHumanInterventionRequest(
    Guid ExecutionId,
    string Type,
    string Title,
    DateTimeOffset ExpiresAt);

public sealed record HumanInterventionDto(
    Guid Id,
    Guid ExecutionId,
    string Type,
    string Status,
    string Title,
    DateTimeOffset ExpiresAt,
    string? QrToken = null);

public sealed record ConsumeQrTokenRequest(string Token);
