using AgentRPA.Domain.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

/// <summary>执行节点及能力关系映射。</summary>
public sealed class ExecutionNodeConfiguration : IEntityTypeConfiguration<ExecutionNode>
{
    public void Configure(EntityTypeBuilder<ExecutionNode> builder)
    {
        builder.ToTable("execution_nodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AgentKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.NodeKind).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.OsPlatform).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Architecture).HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.NetworkZone).HasMaxLength(128);
        builder.Property(x => x.AgentVersion).HasMaxLength(64);
        builder.HasIndex(x => x.AgentKey).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.NodePoolId);
        builder.HasIndex(x => x.LastHeartbeatAt);
    }
}
