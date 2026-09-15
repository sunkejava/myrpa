using AgentRPA.Application.Permission;
using AgentRPA.Domain.Permission;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Permission;

/// <summary>EF Core 权限策略查询与管理实现。</summary>
public sealed class EfAccessPolicyRepository(AgentRpaDbContext db) : IAccessPolicyRepository
{
    public Task<bool> ExistsAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken) =>
        db.AccessPolicies.AsNoTracking().AnyAsync(x => x.Enabled && x.SubjectId == subjectId && x.CityId == cityId && x.SystemId == systemId && x.FunctionId == functionId && x.Action == action, cancellationToken);

    public async Task<IReadOnlyList<AccessPolicy>> ListAsync(Guid? subjectId, CancellationToken cancellationToken)
        => await db.AccessPolicies.AsNoTracking().Where(x => !subjectId.HasValue || x.SubjectId == subjectId.Value).OrderBy(x => x.SubjectId).ThenBy(x => x.Action).ToListAsync(cancellationToken);

    public async Task<AccessPolicy> GrantAsync(AccessPolicy policy, CancellationToken cancellationToken)
    {
        var existing = await db.AccessPolicies.SingleOrDefaultAsync(x => x.SubjectId == policy.SubjectId && x.CityId == policy.CityId && x.SystemId == policy.SystemId && x.FunctionId == policy.FunctionId && x.Action == policy.Action, cancellationToken);
        if (existing is not null)
        {
            existing.SetEnabled(true);
            await db.SaveChangesAsync(cancellationToken);
            return existing;
        }
        db.AccessPolicies.Add(policy);
        await db.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public async Task<bool> SetEnabledAsync(Guid policyId, bool enabled, CancellationToken cancellationToken)
    {
        var policy = await db.AccessPolicies.SingleOrDefaultAsync(x => x.Id == policyId, cancellationToken);
        if (policy is null) return false;
        policy.SetEnabled(enabled);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
