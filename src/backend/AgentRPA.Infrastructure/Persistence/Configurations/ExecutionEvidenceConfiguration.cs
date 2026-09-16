using AgentRPA.Domain.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

/// <summary>执行日志和执行产物数据库映射。</summary>
public sealed class ExecutionEvidenceConfiguration : IEntityTypeConfiguration<ExecutionLog>, IEntityTypeConfiguration<ExecutionArtifact>
{
    public void Configure(EntityTypeBuilder<ExecutionLog> builder)
    {
        builder.ToTable("ExecutionLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.StepId).HasMaxLength(128);
        builder.Property(x => x.MetadataJson).HasMaxLength(16000);
        builder.HasIndex(x => new { x.ExecutionId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.ExecutionId, x.Level });
    }

    public void Configure(EntityTypeBuilder<ExecutionArtifact> builder)
    {
        builder.ToTable("ExecutionArtifacts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ArtifactType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(512).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(256);
        builder.Property(x => x.Sha256).HasMaxLength(128);
        builder.HasIndex(x => new { x.ExecutionId, x.CreatedAt });
        builder.HasIndex(x => new { x.TaskItemId, x.CreatedAt });
    }
}
