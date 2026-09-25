using AgentRPA.Application.Permission;
using AgentRPA.Domain.Permission;

namespace AgentRPA.Tests;

public sealed class WorkflowPermissionPreflightTests
{
    [Fact]
    public async Task Nested_step_with_additional_action_requires_both_execute_and_approve()
    {
        var repository = new PolicyRepository("Execute");
        var preflight = new WorkflowPermissionPreflight(new PermissionService(repository));
        const string definition = """
            {"steps":[{"type":"Condition","config":{"then":[{"type":"Click","requiredAction":"Approve","config":{"selector":"#approve"}}]}}]}
            """;

        var result = await preflight.CheckAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), definition, CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Contains("steps[1].then[1]", result.Reason);
        Assert.Contains("Approve", result.Reason);
        Assert.Contains("Execute", repository.CheckedActions);
        Assert.Contains("Approve", repository.CheckedActions);
    }

    [Fact]
    public async Task Every_step_is_allowed_when_all_actions_are_granted()
    {
        var repository = new PolicyRepository("Execute", "Approve");
        var preflight = new WorkflowPermissionPreflight(new PermissionService(repository));
        const string definition = """
            {"steps":[{"type":"Click","requiredAction":"Approve","config":{"selector":"#approve"}}]}
            """;

        var result = await preflight.CheckAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), definition, CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.Equal(new[] { "Execute", "Approve" }, repository.CheckedActions);
    }

    [Theory]
    [InlineData("{\"steps\":[{\"type\":\"Click\",\"requiredAction\":\"Unsupported\"}]}")]
    [InlineData("{\"steps\":[{\"type\":\"Condition\",\"config\":{\"else\":{}}}]}")]
    public async Task Invalid_step_permission_declarations_fail_closed(string definition)
    {
        var repository = new PolicyRepository("Execute");
        var preflight = new WorkflowPermissionPreflight(new PermissionService(repository));

        var result = await preflight.CheckAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), definition, CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Empty(repository.CheckedActions);
    }

    private sealed class PolicyRepository(params string[] allowedActions) : IAccessPolicyRepository
    {
        public List<string> CheckedActions { get; } = [];
        public Task<bool> IsValidScopeAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> ExistsAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken ct)
        {
            CheckedActions.Add(action);
            return Task.FromResult(allowedActions.Contains(action));
        }
        public Task<bool> ExistsThroughRoleAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<AccessPolicy>> ListAsync(Guid? subjectId, CancellationToken ct) => Task.FromResult<IReadOnlyList<AccessPolicy>>([]);
        public Task<AccessPolicy> GrantAsync(AccessPolicy policy, CancellationToken ct) => Task.FromResult(policy);
        public Task<bool> SetEnabledAsync(Guid policyId, bool enabled, CancellationToken ct) => Task.FromResult(false);
    }
}
