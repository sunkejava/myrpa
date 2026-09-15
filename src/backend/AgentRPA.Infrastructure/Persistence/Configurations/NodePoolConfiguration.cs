using AgentRPA.Domain.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

/// <summary>节点池持久化映射。</summary>
public sealed class NodePoolConfiguration : IEntityTypeConfiguration<NodePool>
{
    public void Configure(EntityTypeBuilder<NodePool> builder)
    {
        builder.ToTable("node_pools");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
