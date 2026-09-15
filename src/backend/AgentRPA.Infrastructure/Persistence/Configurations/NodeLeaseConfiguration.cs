using AgentRPA.Domain.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

/// <summary>节点租约持久化映射。</summary>
public sealed class NodeLeaseConfiguration : IEntityTypeConfiguration<NodeLease>
{
    public void Configure(EntityTypeBuilder<NodeLease> builder)
    {
        builder.ToTable("NodeLeases");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExecutionId).IsRequired();
        builder.Property(x => x.NodeId).IsRequired();
        builder.Property(x => x.WorkerSlotId).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.LastHeartbeatAt).IsRequired();
        builder.Property(x => x.Released).IsRequired();
        builder.HasIndex(x => x.ExecutionId);
        builder.HasIndex(x => new { x.WorkerSlotId, x.Released, x.ExpiresAt });
    }
}
