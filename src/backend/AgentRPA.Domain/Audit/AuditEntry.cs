using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.Audit;

/// <summary>统一审计事件，不保存密码、Cookie、Token、PIN 等敏感明文。</summary>
public sealed class AuditEntry : Entity
{
    private AuditEntry() { }
    public AuditEntry(string actor, string action, string resource, string? resourceId, string? result, string? summary)
    { Actor = actor; Action = action; Resource = resource; ResourceId = resourceId; Result = result; Summary = summary; }
    public string Actor { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Resource { get; private set; } = string.Empty;
    public string? ResourceId { get; private set; }
    public string? Result { get; private set; }
    public string? Summary { get; private set; }
}
