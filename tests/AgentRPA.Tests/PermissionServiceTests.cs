using AgentRPA.Application.Permission;
using AgentRPA.Domain.Permission;

namespace AgentRPA.Tests;

public sealed class PermissionServiceTests
{
    [Fact]
    public async Task Direct_permission_allows_exact_scope_and_action()
    {
        var subjectId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var systemId = Guid.NewGuid();
        var functionId = Guid.NewGuid();
        var repository = new FakeAccessPolicyRepository
        {
            DirectResult = true
        };

        var result = await new PermissionService(repository).CheckAsync(
            subjectId, cityId, systemId, functionId, " Execute ", CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.Contains("直接权限", result.Reason);
        Assert.Equal("Execute", repository.LastAction);
    }

    [Fact]
    public async Task Role_permission_is_used_when_direct_permission_is_missing()
    {
        var repository = new FakeAccessPolicyRepository
        {
            RoleResult = true
        };

        var result = await new PermissionService(repository).CheckAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Execute", CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.Contains("角色继承", result.Reason);
    }

    [Fact]
    public async Task Missing_permission_is_denied_by_default()
    {
        var repository = new FakeAccessPolicyRepository();

        var result = await new PermissionService(repository).CheckAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Execute", CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Contains("没有", result.Reason);
    }

    [Fact]
    public async Task Invalid_scope_or_action_is_denied_before_repository_lookup()
    {
        var repository = new FakeAccessPolicyRepository();

        var result = await new PermissionService(repository).CheckAsync(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Execute", CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Contains("用户身份", result.Reason);
        Assert.Equal(0, repository.LookupCount);
    }

    private sealed class FakeAccessPolicyRepository : IAccessPolicyRepository
    {
        public bool DirectResult { get; init; }
        public bool RoleResult { get; init; }
        public int LookupCount { get; private set; }
        public string? LastAction { get; private set; }

        public Task<bool> ExistsAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken)
        {
            LookupCount++;
            LastAction = action;
            return Task.FromResult(DirectResult);
        }

        public Task<bool> ExistsThroughRoleAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken)
        {
            LookupCount++;
            LastAction = action;
            return Task.FromResult(RoleResult);
        }

        public Task<IReadOnlyList<AccessPolicy>> ListAsync(Guid? subjectId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AccessPolicy>>([]);

        public Task<AccessPolicy> GrantAsync(AccessPolicy policy, CancellationToken cancellationToken)
            => Task.FromResult(policy);

        public Task<bool> SetEnabledAsync(Guid policyId, bool enabled, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }
}
