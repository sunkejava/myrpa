using AgentRPA.Domain.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

/// <summary>Worker Slot 持久化映射。</summary>
public sealed class WorkerSlotConfiguration : IEntityTypeConfiguration<WorkerSlot>
{
    public void Configure(EntityTypeBuilder<WorkerSlot> builder)
    {
        builder.ToTable("worker_slots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SlotName).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.NodeId, x.SlotName }).IsUnique();
        builder.HasIndex(x => x.ExecutionId);
        builder.HasIndex(x => x.LeaseExpiresAt);
    }
}
