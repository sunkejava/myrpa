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
    [HttpGet("cities")]
    public async Task<IActionResult> Cities(CancellationToken ct) => Ok(await db.Cities.AsNoTracking()
        .OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.Name, x.Enabled }).ToListAsync(ct));

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
        try { city = new City(request.Code, request.Name); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        if (await db.Cities.AnyAsync(x => x.Code == city.Code, ct)) return Conflict(new { message = "城市编码已存在。" });
        db.Cities.Add(city);
        await db.SaveChangesAsync(ct);
        return Created($"/api/business-resources/cities/{city.Id}", new { city.Id, city.Code, city.Name });
    }

    [Authorize(Roles = "Admin"), HttpPost("cities/{cityId:guid}/systems")]
    public async Task<IActionResult> CreateSystem(Guid cityId, CreateSystemRequest request, CancellationToken ct)
    {
        if (!await db.Cities.AnyAsync(x => x.Id == cityId && x.Enabled, ct)) return NotFound(new { message = "城市不存在或已停用。" });
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

public sealed record CreateCityRequest(string Code, string Name);
public sealed record CreateSystemRequest(string Code, string Name, string? BaseUrl);
public sealed record CreateFunctionRequest(string Code, string Name);
public sealed record SetEnabledRequest(bool Enabled);
