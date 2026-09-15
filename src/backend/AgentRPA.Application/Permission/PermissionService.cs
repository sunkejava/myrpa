using AgentRPA.Domain.Permission;

namespace AgentRPA.Application.Permission;

/// <summary>Agent 执行前权限检查。默认拒绝，只有精确匹配的用户/城市/系统/功能/Action 才允许。</summary>
public sealed class PermissionService(IAccessPolicyRepository repository)
{
    public async Task<PermissionCheckResult> CheckAsync(
        Guid subjectId,
        Guid cityId,
        Guid systemId,
        Guid functionId,
        string action,
        CancellationToken cancellationToken)
    {
        // 权限检查发生在 Task 创建和外部系统执行之前，任何未授权情况均直接阻断。
        if (subjectId == Guid.Empty) return new(false, "未提供有效用户身份，拒绝执行。");
        if (cityId == Guid.Empty || systemId == Guid.Empty || functionId == Guid.Empty)
            return new(false, "业务资源不完整，拒绝执行。");
        if (string.IsNullOrWhiteSpace(action)) return new(false, "执行动作为空，拒绝执行。");

        var allowed = await repository.ExistsAsync(subjectId, cityId, systemId, functionId, action.Trim(), cancellationToken);
        return allowed
            ? new(true, "权限检查通过。")
            : new(false, "当前用户没有该城市/业务系统/业务功能/动作的执行权限。");
    }
}

/// <summary>权限策略仓储抽象，Application 不依赖 EF Core。</summary>
public interface IAccessPolicyRepository
{
    Task<bool> ExistsAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken);
}
