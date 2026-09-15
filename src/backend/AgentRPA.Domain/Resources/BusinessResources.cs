using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.Resources;

/// <summary>城市领域对象。</summary>
public sealed class City : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
}

/// <summary>城市下的业务系统。</summary>
public sealed class BusinessSystem : Entity
{
    public Guid CityId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string BaseUrl { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
}

/// <summary>业务系统中的可自动化业务功能。</summary>
public sealed class BusinessFunction : Entity
{
    public Guid SystemId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool AllowNaturalLanguage { get; private set; } = true;
    public bool AllowBatch { get; private set; }
    public bool AllowExport { get; private set; }
}
