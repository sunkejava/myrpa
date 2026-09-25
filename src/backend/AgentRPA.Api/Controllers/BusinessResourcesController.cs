using AgentRPA.Domain.Resources;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Controllers;

/// <summary>城市、业务系统和功能的管理入口；资源必须沿真实父子关系创建。</summary>
[ApiController, Route("api/business-resources"), Authorize]
public sealed class BusinessResourcesController(AgentRpaDbContext db) : ControllerBase
{
    [HttpGet("countries")]
    public async Task<IActionResult> Countries(CancellationToken ct) => Ok(await db.Countries.AsNoTracking().OrderBy(x => x.Code)
        .Select(x => new { x.Id, x.Code, x.Name, x.Enabled }).ToListAsync(ct));

    [HttpGet("countries/{countryId:guid}/provinces")]
    public async Task<IActionResult> Provinces(Guid countryId, CancellationToken ct) => Ok(await db.Provinces.AsNoTracking()
        .Where(x => x.CountryId == countryId).OrderBy(x => x.Code)
        .Select(x => new { x.Id, x.CountryId, x.Code, x.Name, x.Enabled }).ToListAsync(ct));

    [HttpGet("provinces/{provinceId:guid}/cities")]
    public async Task<IActionResult> ProvinceCities(Guid provinceId, CancellationToken ct) => Ok(await db.Cities.AsNoTracking()
        .Where(x => x.ProvinceId == provinceId).OrderBy(x => x.Code)
        .Select(x => new { x.Id, x.ProvinceId, x.Code, x.Name, x.Enabled }).ToListAsync(ct));

    [HttpGet("cities/{cityId:guid}/districts")]
    public async Task<IActionResult> Districts(Guid cityId, CancellationToken ct) => Ok(await db.Districts.AsNoTracking()
        .Where(x => x.CityId == cityId).OrderBy(x => x.Code)
        .Select(x => new { x.Id, x.CityId, x.Code, x.Name, x.Enabled }).ToListAsync(ct));

    [Authorize(Roles = "Admin"), HttpPost("countries")]
    public async Task<IActionResult> CreateCountry(CreateRegionRequest request, CancellationToken ct)
    {
        Country country;
        try { country = new Country(request.Code, request.Name); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (await db.Countries.AnyAsync(x => x.Code == country.Code, ct)) return Conflict(new { message = "国家编码已存在。" });
        db.Countries.Add(country); await db.SaveChangesAsync(ct);
        return Created($"/api/business-resources/countries/{country.Id}", new { country.Id, country.Code, country.Name });
    }

    [Authorize(Roles = "Admin"), HttpPost("countries/{countryId:guid}/provinces")]
    public async Task<IActionResult> CreateProvince(Guid countryId, CreateRegionRequest request, CancellationToken ct)
    {
        if (!await db.Countries.AnyAsync(x => x.Id == countryId && x.Enabled, ct)) return NotFound();
        Province province;
        try { province = new Province(countryId, request.Code, request.Name); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (await db.Provinces.AnyAsync(x => x.CountryId == countryId && x.Code == province.Code, ct)) return Conflict(new { message = "省份编码已存在。" });
        db.Provinces.Add(province); await db.SaveChangesAsync(ct);
        return Created($"/api/business-resources/countries/{countryId}/provinces", new { province.Id, province.CountryId, province.Code, province.Name });
    }

    [Authorize(Roles = "Admin"), HttpPost("cities/{cityId:guid}/districts")]
    public async Task<IActionResult> CreateDistrict(Guid cityId, CreateRegionRequest request, CancellationToken ct)
    {
        if (!await db.Cities.AnyAsync(x => x.Id == cityId && x.Enabled, ct)) return NotFound();
        District district;
        try { district = new District(cityId, request.Code, request.Name); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (await db.Districts.AnyAsync(x => x.CityId == cityId && x.Code == district.Code, ct)) return Conflict(new { message = "区县编码已存在。" });
        db.Districts.Add(district); await db.SaveChangesAsync(ct);
        return Created($"/api/business-resources/cities/{cityId}/districts", new { district.Id, district.CityId, district.Code, district.Name });
    }

    [Authorize(Roles = "Admin"), HttpPost("countries/{countryId:guid}/enabled")]
    public async Task<IActionResult> SetCountryEnabled(Guid countryId, SetEnabledRequest request, CancellationToken ct)
    {
        var country = await db.Countries.FindAsync([countryId], ct);
        if (country is null) return NotFound();
        country.SetEnabled(request.Enabled); await db.SaveChangesAsync(ct);
        return Ok(new { country.Id, country.Enabled });
    }

    [Authorize(Roles = "Admin"), HttpPost("provinces/{provinceId:guid}/enabled")]
    public async Task<IActionResult> SetProvinceEnabled(Guid provinceId, SetEnabledRequest request, CancellationToken ct)
    {
        var province = await db.Provinces.FindAsync([provinceId], ct);
        if (province is null) return NotFound();
        province.SetEnabled(request.Enabled); await db.SaveChangesAsync(ct);
        return Ok(new { province.Id, province.Enabled });
    }

    [Authorize(Roles = "Admin"), HttpPost("districts/{districtId:guid}/enabled")]
    public async Task<IActionResult> SetDistrictEnabled(Guid districtId, SetEnabledRequest request, CancellationToken ct)
    {
        var district = await db.Districts.FindAsync([districtId], ct);
        if (district is null) return NotFound();
        district.SetEnabled(request.Enabled); await db.SaveChangesAsync(ct);
        return Ok(new { district.Id, district.Enabled });
    }
    [HttpGet("cities")]
    public async Task<IActionResult> Cities(CancellationToken ct) => Ok(await db.Cities.AsNoTracking()
        .OrderBy(x => x.Code).Select(x => new { x.Id, x.ProvinceId, x.Code, x.Name, x.Enabled }).ToListAsync(ct));

    [HttpGet("cities/{cityId:guid}/systems")]
    public async Task<IActionResult> Systems(Guid cityId, CancellationToken ct) => Ok(await db.BusinessSystems.AsNoTracking()
        .Where(x => x.CityId == cityId).OrderBy(x => x.Code)
        .Select(x => new { x.Id, x.CityId, x.Code, x.Name, x.BaseUrl, x.Enabled }).ToListAsync(ct));

    [HttpGet("systems/{systemId:guid}/functions")]
    public async Task<IActionResult> Functions(Guid systemId, CancellationToken ct) => Ok(await db.BusinessFunctions.AsNoTracking()
        .Where(x => x.SystemId == systemId).OrderBy(x => x.Code)
        .Select(x => new { x.Id, x.SystemId, x.Code, x.Name }).ToListAsync(ct));

    [Authorize(Roles = "Admin"), HttpPost("cities")]
    public async Task<IActionResult> CreateCity(CreateCityRequest request, CancellationToken ct)
    {
        City city;
        if (request.ProvinceId.HasValue && !await db.Provinces.AnyAsync(x => x.Id == request.ProvinceId && x.Enabled && db.Countries.Any(c => c.Id == x.CountryId && c.Enabled), ct))
            return BadRequest(new { message = "省份或国家不存在或已停用。" });
        try { city = new City(request.Code, request.Name, request.ProvinceId); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (await db.Cities.AnyAsync(x => x.Code == city.Code, ct)) return Conflict(new { message = "城市编码已存在。" });
        db.Cities.Add(city);
        await db.SaveChangesAsync(ct);
        return Created($"/api/business-resources/cities/{city.Id}", new { city.Id, city.Code, city.Name });
    }

    [Authorize(Roles = "Admin"), HttpPost("cities/{cityId:guid}/systems")]
    public async Task<IActionResult> CreateSystem(Guid cityId, CreateSystemRequest request, CancellationToken ct)
    {
        if (!await db.Cities.AnyAsync(x => x.Id == cityId && x.Enabled &&
            (!x.ProvinceId.HasValue || db.Provinces.Any(p => p.Id == x.ProvinceId && p.Enabled &&
                db.Countries.Any(c => c.Id == p.CountryId && c.Enabled))), ct)) return NotFound(new { message = "城市或上级区域不存在或已停用。" });
        BusinessSystem system;
        try { system = new BusinessSystem(cityId, request.Code, request.Name, request.BaseUrl); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (await db.BusinessSystems.AnyAsync(x => x.CityId == cityId && x.Code == system.Code, ct)) return Conflict(new { message = "该城市的系统编码已存在。" });
        db.BusinessSystems.Add(system);
        await db.SaveChangesAsync(ct);
        return Created($"/api/business-resources/cities/{cityId}/systems", new { system.Id, system.CityId, system.Code, system.Name });
    }

    [Authorize(Roles = "Admin"), HttpPost("systems/{systemId:guid}/functions")]
    public async Task<IActionResult> CreateFunction(Guid systemId, CreateFunctionRequest request, CancellationToken ct)
    {
        if (!await db.BusinessSystems.AnyAsync(x => x.Id == systemId && x.Enabled && db.Cities.Any(c => c.Id == x.CityId && c.Enabled), ct))
            return NotFound(new { message = "系统不存在或已停用。" });
        BusinessFunction function;
        try { function = new BusinessFunction(systemId, request.Code, request.Name); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (await db.BusinessFunctions.AnyAsync(x => x.SystemId == systemId && x.Code == function.Code, ct)) return Conflict(new { message = "该系统的功能编码已存在。" });
        db.BusinessFunctions.Add(function);
        await db.SaveChangesAsync(ct);
        return Created($"/api/business-resources/systems/{systemId}/functions", new { function.Id, function.SystemId, function.Code, function.Name });
    }

    [Authorize(Roles = "Admin"), HttpPost("cities/{cityId:guid}/enabled")]
    public async Task<IActionResult> SetCityEnabled(Guid cityId, SetEnabledRequest request, CancellationToken ct)
    {
        var city = await db.Cities.FindAsync([cityId], ct);
        if (city is null) return NotFound();
        city.SetEnabled(request.Enabled);
        await db.SaveChangesAsync(ct);
        return Ok(new { city.Id, city.Enabled });
    }

    [Authorize(Roles = "Admin"), HttpPost("systems/{systemId:guid}/enabled")]
    public async Task<IActionResult> SetSystemEnabled(Guid systemId, SetEnabledRequest request, CancellationToken ct)
    {
        var system = await db.BusinessSystems.FindAsync([systemId], ct);
        if (system is null) return NotFound();
        system.SetEnabled(request.Enabled);
        await db.SaveChangesAsync(ct);
        return Ok(new { system.Id, system.Enabled });
    }
}

public sealed record CreateCityRequest(string Code, string Name, Guid? ProvinceId = null);
public sealed record CreateRegionRequest(string Code, string Name);
public sealed record CreateSystemRequest(string Code, string Name, string? BaseUrl);
public sealed record CreateFunctionRequest(string Code, string Name);
public sealed record SetEnabledRequest(bool Enabled);
