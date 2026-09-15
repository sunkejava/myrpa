using AgentRPA.Domain.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

/// <summary>独占资源锁持久化映射。</summary>
public sealed class ResourceLockConfiguration : IEntityTypeConfiguration<ResourceLock>
{
    public void Configure(EntityTypeBuilder<ResourceLock> builder)
    {
        builder.ToTable("ResourceLocks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ResourceType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResourceId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ExecutionId).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.LastHeartbeatAt).IsRequired();
        builder.Property(x => x.Released).IsRequired();
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId, x.Released, x.ExpiresAt });
        builder.HasIndex(x => x.ExecutionId);
    }
}
