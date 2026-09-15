using AgentRPA.Application.Agent;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Agent;

/// <summary>EF Core 资源目录实现，Infrastructure 负责把数据库资源提供给 Agent Application。</summary>
public sealed class EfAgentResourceCatalog(AgentRpaDbContext db) : IAgentResourceCatalog
{
    public async Task<IReadOnlyList<AgentCityResource>> GetCitiesAsync(CancellationToken cancellationToken) =>
        await db.Cities.AsNoTracking().Select(x => new AgentCityResource(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AgentSystemResource>> GetSystemsAsync(CancellationToken cancellationToken) =>
        await db.BusinessSystems.AsNoTracking().Select(x => new AgentSystemResource(x.Id, x.CityId, x.Name, x.Code)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AgentFunctionResource>> GetFunctionsAsync(CancellationToken cancellationToken) =>
        await db.BusinessFunctions.AsNoTracking().Select(x => new AgentFunctionResource(x.Id, x.BusinessSystemId, x.Name, x.Code)).ToListAsync(cancellationToken);
}
