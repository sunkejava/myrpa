using AgentRPA.Domain.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

/// <summary>节点能力持久化映射。</summary>
public sealed class NodeCapabilityConfiguration : IEntityTypeConfiguration<NodeCapability>
{
    public void Configure(EntityTypeBuilder<NodeCapability> builder)
    {
        builder.ToTable("node_capabilities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(64);
        builder.Property(x => x.MetadataJson).HasColumnType("TEXT");
        builder.HasIndex(x => new { x.NodeId, x.Code }).IsUnique();
        builder.HasIndex(x => x.Code);
    }
}
