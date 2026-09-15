using AgentRPA.Domain.Permission;

namespace AgentRPA.Application.Permission;

/// <summary>权限策略管理应用服务，统一校验四级资源范围并避免重复授权。</summary>
public sealed class PermissionManagementService(IAccessPolicyRepository repository)
{
    public Task<IReadOnlyList<AccessPolicy>> ListAsync(Guid? subjectId, CancellationToken cancellationToken)
        => repository.ListAsync(subjectId, cancellationToken);

    public Task<AccessPolicy> GrantAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken)
    {
        Validate(subjectId, cityId, systemId, functionId, action);
        return repository.GrantAsync(new AccessPolicy(subjectId, cityId, systemId, functionId, action.Trim()), cancellationToken);
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
