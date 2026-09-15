namespace AgentRPA.Contracts.Nodes;

public sealed record NodeRegistrationResponse(Guid NodeId, string AgentKey, string Status);
