using AgentRPA.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Actor).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Resource).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ResourceId).HasMaxLength(128);
        builder.Property(x => x.Result).HasMaxLength(32);
        builder.Property(x => x.Summary).HasMaxLength(2000);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.Actor, x.Resource });
    }
}
