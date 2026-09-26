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
        // 北京医保只初始化资源目录，不填入未经验证的政务地址、账号或个人资料。
        var country = await db.Countries.SingleOrDefaultAsync(x => x.Code == "CN", ct);
        if (country is null) { country = new Country("CN", "中国"); db.Countries.Add(country); }
        var province = await db.Provinces.SingleOrDefaultAsync(x => x.CountryId == country.Id && x.Code == "BJ", ct);
        if (province is null) { province = new Province(country.Id, "BJ", "北京市"); db.Provinces.Add(province); }
        var beijing = await db.Cities.SingleOrDefaultAsync(x => x.Code == "CN-BJ", ct);
        if (beijing is null) { beijing = new City("CN-BJ", "北京市", province.Id); db.Cities.Add(beijing); }
        var insurance = await db.BusinessSystems.SingleOrDefaultAsync(x => x.CityId == beijing.Id && x.Code == "BJ-MEDICAL", ct);
        if (insurance is null)
        {
            insurance = new BusinessSystem(beijing.Id, "BJ-MEDICAL", "北京医保业务系统（待配置地址）", null);
            db.BusinessSystems.Add(insurance);
        }
        if (!await db.BusinessFunctions.AnyAsync(x => x.SystemId == insurance.Id && x.Code == "PERSON-QUERY", ct))
            db.BusinessFunctions.Add(new BusinessFunction(insurance.Id, "PERSON-QUERY", "人员信息查询与下载"));
        await db.SaveChangesAsync(ct);
    }
}
