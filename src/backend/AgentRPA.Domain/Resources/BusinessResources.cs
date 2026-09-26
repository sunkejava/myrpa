using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.Resources;

/// <summary>国家及省份资源节点；旧城市允许暂不绑定省份以兼容历史数据。</summary>
public sealed class Country : Entity
{
    private Country() { }
    public Country(string code, string name) { Code = City.Require(code, 32, nameof(code)).ToUpperInvariant(); Name = City.Require(name, 128, nameof(name)); }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public void SetEnabled(bool enabled) { Enabled = enabled; UpdatedAt = DateTimeOffset.UtcNow; }
}

public sealed class Province : Entity
{
    private Province() { }
    public Province(Guid countryId, string code, string name)
    {
        if (countryId == Guid.Empty) throw new ArgumentException("国家不能为空。", nameof(countryId));
        CountryId = countryId; Code = City.Require(code, 32, nameof(code)).ToUpperInvariant(); Name = City.Require(name, 128, nameof(name));
    }
    public Guid CountryId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public void SetEnabled(bool enabled) { Enabled = enabled; UpdatedAt = DateTimeOffset.UtcNow; }
}

public sealed class District : Entity
{
    private District() { }
    public District(Guid cityId, string code, string name)
    {
        if (cityId == Guid.Empty) throw new ArgumentException("城市不能为空。", nameof(cityId));
        CityId = cityId; Code = City.Require(code, 32, nameof(code)).ToUpperInvariant(); Name = City.Require(name, 128, nameof(name));
    }
    public Guid CityId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public void SetEnabled(bool enabled) { Enabled = enabled; UpdatedAt = DateTimeOffset.UtcNow; }
}

/// <summary>城市领域对象。</summary>
public sealed class City : Entity
{
    private City() { }
    public City(string code, string name, Guid? provinceId = null)
    {
        if (provinceId == Guid.Empty) throw new ArgumentException("省份不能为空。", nameof(provinceId));
        ProvinceId = provinceId;
        Code = Require(code, 32, nameof(code)).ToUpperInvariant();
        Name = Require(name, 128, nameof(name));
    }
    public string Code { get; private set; } = string.Empty;
    public Guid? ProvinceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public void SetEnabled(bool enabled) { Enabled = enabled; UpdatedAt = DateTimeOffset.UtcNow; }

    internal static string Require(string value, int maxLength, string parameter) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength
            ? throw new ArgumentException($"{parameter} 不能为空且不得超过 {maxLength} 字符。", parameter)
            : value.Trim();
}

/// <summary>城市下的业务系统。</summary>
public sealed class BusinessSystem : Entity
{
    private BusinessSystem() { }
    public BusinessSystem(Guid cityId, string code, string name, string? baseUrl)
    {
        if (cityId == Guid.Empty) throw new ArgumentException("城市不能为空。", nameof(cityId));
        CityId = cityId;
        Code = City.Require(code, 64, nameof(code)).ToUpperInvariant();
        Name = City.Require(name, 128, nameof(name));
        SetBaseUrl(baseUrl);
    }
    public Guid CityId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string BaseUrl { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public void SetEnabled(bool enabled) { Enabled = enabled; UpdatedAt = DateTimeOffset.UtcNow; }
    public void SetBaseUrl(string? baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl) &&
            (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
            throw new ArgumentException("系统地址必须是有效的 HTTP 或 HTTPS 绝对地址。", nameof(baseUrl));
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : City.Require(baseUrl, 1024, nameof(baseUrl));
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>业务系统中的可自动化业务功能。</summary>
public sealed class BusinessFunction : Entity
{
    private BusinessFunction() { }
    public BusinessFunction(Guid systemId, string code, string name)
    {
        if (systemId == Guid.Empty) throw new ArgumentException("业务系统不能为空。", nameof(systemId));
        SystemId = systemId;
        Code = City.Require(code, 64, nameof(code)).ToUpperInvariant();
        Name = City.Require(name, 128, nameof(name));
    }
    public Guid SystemId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool AllowNaturalLanguage { get; private set; } = true;
    public bool AllowBatch { get; private set; }
    public bool AllowExport { get; private set; }
}
