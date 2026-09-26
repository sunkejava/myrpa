using AgentRPA.Domain.Resources;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Seeding;

/// <summary>开发数据库的可重复执行演示资源。管理员账户由 IdentityBootstrapper 创建。</summary>
public static class DevelopmentSeedData
{
    public static async Task SeedAsync(AgentRpaDbContext db, CancellationToken ct = default)
    {
        var city = await db.Cities.SingleOrDefaultAsync(x => x.Code == "DEMO-QD", ct);
        if (city is null)
        {
            city = new City("DEMO-QD", "演示城市·青岛");
            db.Cities.Add(city);
        }
        var system = await db.BusinessSystems.SingleOrDefaultAsync(x => x.CityId == city.Id && x.Code == "DEMO-SOCIAL", ct);
        if (system is null)
        {
            system = new BusinessSystem(city.Id, "DEMO-SOCIAL", "演示社保系统", null);
            db.BusinessSystems.Add(system);
        }
        if (!await db.BusinessFunctions.AnyAsync(x => x.SystemId == system.Id && x.Code == "DEMO-QUERY", ct))
            db.BusinessFunctions.Add(new BusinessFunction(system.Id, "DEMO-QUERY", "参保状态查询"));
        await db.SaveChangesAsync(ct);
    }
}
