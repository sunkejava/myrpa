using AgentRPA.Application.Agent;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Agent;

/// <summary>EF Core 资源目录实现，Infrastructure 负责把数据库资源提供给 Agent Application。</summary>
public sealed class EfAgentResourceCatalog(AgentRpaDbContext db) : IAgentResourceCatalog
{
    public async Task<IReadOnlyList<AgentCityResource>> GetCitiesAsync(CancellationToken cancellationToken) =>
        await db.Cities.AsNoTracking().Where(x => x.Enabled && (!x.ProvinceId.HasValue ||
            db.Provinces.Any(p => p.Id == x.ProvinceId && p.Enabled && db.Countries.Any(c => c.Id == p.CountryId && c.Enabled))))
            .Select(x => new AgentCityResource(x.Id, x.Name, x.Code)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AgentSystemResource>> GetSystemsAsync(CancellationToken cancellationToken) =>
        await db.BusinessSystems.AsNoTracking().Where(x => x.Enabled && db.Cities.Any(c => c.Id == x.CityId && c.Enabled &&
            (!c.ProvinceId.HasValue || db.Provinces.Any(p => p.Id == c.ProvinceId && p.Enabled && db.Countries.Any(country => country.Id == p.CountryId && country.Enabled)))))
            .Select(x => new AgentSystemResource(x.Id, x.CityId, x.Name, x.Code)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AgentFunctionResource>> GetFunctionsAsync(CancellationToken cancellationToken) =>
        await db.BusinessFunctions.AsNoTracking()
            .Where(x => db.BusinessSystems.Any(s => s.Id == x.SystemId && s.Enabled && db.Cities.Any(c => c.Id == s.CityId && c.Enabled &&
                (!c.ProvinceId.HasValue || db.Provinces.Any(p => p.Id == c.ProvinceId && p.Enabled && db.Countries.Any(country => country.Id == p.CountryId && country.Enabled))))))
            .Select(x => new AgentFunctionResource(x.Id, x.SystemId, x.Name, x.Code)).ToListAsync(cancellationToken);
}
