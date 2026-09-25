using AgentRPA.Domain.Permission;

namespace AgentRPA.Application.Permission;

/// <summary>执行前权限检查。用户直授权限与启用角色权限均可授权，默认拒绝。</summary>
public sealed class PermissionService(IAccessPolicyRepository repository)
{
    public async Task<PermissionCheckResult> CheckAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken)
    {
        if (subjectId == Guid.Empty) return new(false, "未提供有效用户身份，拒绝执行。");
        if (cityId == Guid.Empty || systemId == Guid.Empty || functionId == Guid.Empty) return new(false, "业务资源不完整，拒绝执行。");
        if (!PermissionScopeRules.IsValidAction(action)) return new(false, "执行动作无效，拒绝执行。");
        if (!await repository.IsValidScopeAsync(subjectId, cityId, systemId, functionId, cancellationToken))
            return new(false, "用户或业务资源已停用，或城市、系统、功能的归属关系无效。");
        var normalizedAction = action.Trim();
        if (await repository.ExistsAsync(subjectId, cityId, systemId, functionId, normalizedAction, cancellationToken)) return new(true, "用户直接权限检查通过。");
        if (await repository.ExistsThroughRoleAsync(subjectId, cityId, systemId, functionId, normalizedAction, cancellationToken)) return new(true, "角色继承权限检查通过。");
        return new(false, "当前用户没有该城市/业务系统/业务功能/动作的执行权限。");
    }
}

/// <summary>权限策略仓储抽象，Application 不依赖 EF Core。</summary>
public interface IAccessPolicyRepository
{
    Task<bool> IsValidScopeAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken);
    Task<bool> ExistsThroughRoleAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccessPolicy>> ListAsync(Guid? subjectId, CancellationToken cancellationToken);
    Task<AccessPolicy> GrantAsync(AccessPolicy policy, CancellationToken cancellationToken);
    Task<bool> SetEnabledAsync(Guid policyId, bool enabled, CancellationToken cancellationToken);
}
