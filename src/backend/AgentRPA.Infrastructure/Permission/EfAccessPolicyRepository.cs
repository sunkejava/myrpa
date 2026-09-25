using AgentRPA.Application.Permission;
using AgentRPA.Domain.Permission;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Permission;

/// <summary>EF Core 权限策略查询与管理实现。</summary>
public sealed class EfAccessPolicyRepository(AgentRpaDbContext db) : IAccessPolicyRepository
{
    public async Task<bool> IsValidScopeAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, CancellationToken cancellationToken) =>
        await db.UserAccounts.AsNoTracking().AnyAsync(x => x.Id == subjectId && x.Enabled, cancellationToken) &&
        await db.Cities.AsNoTracking().AnyAsync(x => x.Id == cityId && x.Enabled, cancellationToken) &&
        await db.BusinessSystems.AsNoTracking().AnyAsync(x => x.Id == systemId && x.CityId == cityId && x.Enabled, cancellationToken) &&
        await db.BusinessFunctions.AsNoTracking().AnyAsync(x => x.Id == functionId && x.SystemId == systemId, cancellationToken);

    public Task<bool> ExistsAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken) =>
        db.AccessPolicies.AsNoTracking().AnyAsync(x => x.Enabled && x.SubjectId == subjectId && x.CityId == cityId && x.SystemId == systemId && x.FunctionId == functionId && x.Action == action, cancellationToken);

    public Task<bool> ExistsThroughRoleAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken) =>
        (from ur in db.UserRoles.AsNoTracking()
         join role in db.Roles.AsNoTracking() on ur.RoleId equals role.Id
         join policy in db.RoleAccessPolicies.AsNoTracking() on role.Id equals policy.RoleId
         where ur.UserAccountId == subjectId && role.Enabled && policy.Enabled && policy.CityId == cityId && policy.SystemId == systemId && policy.FunctionId == functionId && policy.Action == action
         select policy.Id).AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<AccessPolicy>> ListAsync(Guid? subjectId, CancellationToken cancellationToken)
        => await db.AccessPolicies.AsNoTracking().Where(x => !subjectId.HasValue || x.SubjectId == subjectId.Value).OrderBy(x => x.SubjectId).ThenBy(x => x.Action).ToListAsync(cancellationToken);

    public async Task<AccessPolicy> GrantAsync(AccessPolicy policy, CancellationToken cancellationToken)
    {
        var existing = await db.AccessPolicies.SingleOrDefaultAsync(x => x.SubjectId == policy.SubjectId && x.CityId == policy.CityId && x.SystemId == policy.SystemId && x.FunctionId == policy.FunctionId && x.Action == policy.Action, cancellationToken);
        if (existing is not null) { existing.SetEnabled(true); await db.SaveChangesAsync(cancellationToken); return existing; }
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
