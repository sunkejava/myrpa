using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.Permission;

/// <summary>细粒度业务访问策略：用户/角色可被授权到城市、系统、功能和动作。</summary>
public sealed class AccessPolicy : Entity
{
    private AccessPolicy() { }

    public AccessPolicy(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, bool denied = false)
    {
        SubjectId = subjectId;
        CityId = cityId;
        SystemId = systemId;
        FunctionId = functionId;
        Action = action;
        Denied = denied;
    }

    public Guid SubjectId { get; private set; }
    public Guid CityId { get; private set; }
    public Guid SystemId { get; private set; }
    public Guid FunctionId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public bool Denied { get; private set; }

    public void SetEnabled(bool enabled) => Enabled = enabled;
    public void SetDenied(bool denied) => Denied = denied;
}

/// <summary>权限检查结果，明确记录拒绝原因，便于 API 和审计层复用。</summary>
public sealed record PermissionCheckResult(bool Allowed, string Reason);

/// <summary>权限策略只允许精确的四级资源 + Action 匹配，不提供通配权限，避免越权扩大。</summary>
public static class PermissionScopeRules
{
    public static bool IsValidAction(string action) => !string.IsNullOrWhiteSpace(action) && action.Trim().Length <= 64;
}
