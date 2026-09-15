using AgentRPA.Domain.Permission;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

/// <summary>细粒度权限策略持久化配置。</summary>
public sealed class PermissionConfiguration : IEntityTypeConfiguration<AccessPolicy>
{
    public void Configure(EntityTypeBuilder<AccessPolicy> builder)
    {
        builder.ToTable("access_policies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.SubjectId, x.CityId, x.SystemId, x.FunctionId, x.Action }).IsUnique();
    }
}
