using AgentRPA.Domain.Permission;

namespace AgentRPA.Application.Permission;

/// <summary>权限策略管理应用服务，统一校验四级资源范围并避免重复授权。</summary>
public sealed class PermissionManagementService(IAccessPolicyRepository repository)
{
    public Task<IReadOnlyList<AccessPolicy>> ListAsync(Guid? subjectId, CancellationToken cancellationToken)
        => repository.ListAsync(subjectId, cancellationToken);

    public async Task<AccessPolicy> GrantAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken)
        => await SetPolicyAsync(subjectId, cityId, systemId, functionId, action, denied: false, cancellationToken: cancellationToken);

    public async Task<AccessPolicy> DenyAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken)
        => await SetPolicyAsync(subjectId, cityId, systemId, functionId, action, denied: true, cancellationToken: cancellationToken);

    private async Task<AccessPolicy> SetPolicyAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, bool denied, CancellationToken cancellationToken)
    {
        Validate(subjectId, cityId, systemId, functionId, action);
        if (!await repository.IsValidScopeAsync(subjectId, cityId, systemId, functionId, cancellationToken))
            throw new ArgumentException("用户或城市、系统、功能之间的资源关系无效。");
        return await repository.GrantAsync(new AccessPolicy(subjectId, cityId, systemId, functionId, action.Trim(), denied), cancellationToken);
    }

    public Task<bool> RevokeAsync(Guid policyId, CancellationToken cancellationToken)
        => repository.SetEnabledAsync(policyId, false, cancellationToken);

    private static void Validate(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action)
    {
        if (subjectId == Guid.Empty) throw new ArgumentException("用户主体不能为空。", nameof(subjectId));
        if (cityId == Guid.Empty || systemId == Guid.Empty || functionId == Guid.Empty)
            throw new ArgumentException("城市、业务系统和业务功能必须全部指定。");
        if (!PermissionScopeRules.IsValidAction(action))
            throw new ArgumentException("Action 不能为空且长度不能超过 64 个字符。", nameof(action));
    }
}
