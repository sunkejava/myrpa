using AgentRPA.Domain.Resources;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Seeding;

/// <summary>启动时补齐基础业务目录；使用自然编码匹配，不覆盖管理员维护的状态和地址。</summary>
public static class DefaultResourceSeedData
{
    private static readonly (string Code, string Name)[] Functions =
    [
        ("PERSON-ADD", "人员增加"),
        ("PERSON-REMOVE", "人员减少"),
        ("PERSON-ROSTER", "人员花名册"),
        ("UNIT-CERTIFICATE", "单位参保证明"),
        ("PERSON-CERTIFICATE", "个人参保证明")
    ];

    public static async Task SeedAsync(AgentRpaDbContext db, CancellationToken ct = default)
    {
        var country = await db.Countries.SingleOrDefaultAsync(x => x.Code == "CN", ct);
        if (country is null) { country = new Country("CN", "中国"); db.Countries.Add(country); }

        await SeedCityAsync("BJ", "北京市", "CN-BJ", "北京市",
            [("DONGCHENG", "东城区"), ("XICHENG", "西城区"), ("CHAOYANG", "朝阳区"), ("HAIDIAN", "海淀区")]);
        await SeedCityAsync("SD", "山东省", "CN-SD-QD", "青岛市",
            [("SHINAN", "市南区"), ("SHIBEI", "市北区"), ("LAOSHAN", "崂山区")]);
        await db.SaveChangesAsync(ct);

        async Task SeedCityAsync(string provinceCode, string provinceName, string cityCode, string cityName,
            (string Code, string Name)[] districtDefinitions)
        {
            var province = await db.Provinces.SingleOrDefaultAsync(x => x.CountryId == country.Id && x.Code == provinceCode, ct);
            if (province is null) { province = new Province(country.Id, provinceCode, provinceName); db.Provinces.Add(province); }
            var city = await db.Cities.SingleOrDefaultAsync(x => x.Code == cityCode, ct);
            if (city is null) { city = new City(cityCode, cityName, province.Id); db.Cities.Add(city); }
            foreach (var (code, name) in districtDefinitions)
                if (!await db.Districts.AnyAsync(x => x.CityId == city.Id && x.Code == code, ct))
                    db.Districts.Add(new District(city.Id, code, name));

            foreach (var (suffix, systemName) in new[] { ("SOCIAL", "社保业务系统"), ("MEDICAL", "医保业务系统"), ("HOUSING", "公积金业务系统") })
            {
                var systemCode = (provinceCode == "BJ" ? "BJ" : "QD") + "-" + suffix;
                var system = await db.BusinessSystems.SingleOrDefaultAsync(x => x.CityId == city.Id && x.Code == systemCode, ct);
                if (system is null)
                {
                    system = new BusinessSystem(city.Id, systemCode, systemName, null);
                    db.BusinessSystems.Add(system);
                }
                foreach (var (code, name) in Functions)
                    if (!await db.BusinessFunctions.AnyAsync(x => x.SystemId == system.Id && x.Code == code, ct))
                        db.BusinessFunctions.Add(new BusinessFunction(system.Id, code, name));
                if (systemCode == "BJ-MEDICAL" && !await db.BusinessFunctions.AnyAsync(x => x.SystemId == system.Id && x.Code == "PERSON-QUERY", ct))
                    db.BusinessFunctions.Add(new BusinessFunction(system.Id, "PERSON-QUERY", "人员信息查询与下载"));
            }
        }
    }
}
