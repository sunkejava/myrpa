using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.Resources;

/// <summary>城市领域对象。</summary>
public sealed class City : Entity
{
    private City() { }
    public City(string code, string name)
    {
        Code = Require(code, 32, nameof(code)).ToUpperInvariant();
        Name = Require(name, 128, nameof(name));
    }
    public string Code { get; private set; } = string.Empty;
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
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : City.Require(baseUrl, 1024, nameof(baseUrl));
    }
    public Guid CityId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string BaseUrl { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public void SetEnabled(bool enabled) { Enabled = enabled; UpdatedAt = DateTimeOffset.UtcNow; }
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
