namespace AgentRPA.Application.Agent;

public sealed record AgentCityResource(Guid Id, string Name, string Code);
public sealed record AgentSystemResource(Guid Id, Guid CityId, string Name, string Code);
public sealed record AgentFunctionResource(Guid Id, Guid SystemId, string Name, string Code);

/// <summary>Agent 资源目录抽象，Application 不依赖数据库实现。</summary>
public interface IAgentResourceCatalog
{
    Task<IReadOnlyList<AgentCityResource>> GetCitiesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentSystemResource>> GetSystemsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentFunctionResource>> GetFunctionsAsync(CancellationToken cancellationToken);
}
