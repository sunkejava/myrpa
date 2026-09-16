using AgentRPA.Domain.HumanIntervention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

/// <summary>人工介入请求持久化映射。</summary>
public sealed class HumanInterventionConfiguration : IEntityTypeConfiguration<HumanIntervention>
{
    public void Configure(EntityTypeBuilder<HumanIntervention> builder)
    {
        builder.ToTable("HumanInterventions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SecureEntryHash).HasMaxLength(128);
        builder.HasIndex(x => new { x.ExecutionId, x.Status });
        builder.HasIndex(x => new { x.SubjectId, x.Status });
        builder.HasIndex(x => x.SecureEntryHash);
    }
}
