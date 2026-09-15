using AgentRPA.Application.Permission;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Permission;

/// <summary>EF Core 权限策略查询实现。</summary>
public sealed class EfAccessPolicyRepository(AgentRpaDbContext db) : IAccessPolicyRepository
{
    public Task<bool> ExistsAsync(Guid subjectId, Guid cityId, Guid systemId, Guid functionId, string action, CancellationToken cancellationToken) =>
        db.AccessPolicies.AsNoTracking().AnyAsync(x =>
            x.Enabled && x.SubjectId == subjectId && x.CityId == cityId && x.SystemId == systemId &&
            x.FunctionId == functionId && x.Action == action, cancellationToken);
}
