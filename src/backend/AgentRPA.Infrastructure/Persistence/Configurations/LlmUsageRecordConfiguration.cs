using AgentRPA.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

public sealed class LlmUsageRecordConfiguration : IEntityTypeConfiguration<LlmUsageRecord>
{
    public void Configure(EntityTypeBuilder<LlmUsageRecord> builder)
    {
        builder.ToTable("LlmUsageRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.SubjectId);
        builder.HasIndex(x => x.TaskId);
        builder.HasIndex(x => x.TaskItemId);
        // SQLite 不允许直接按 DateTimeOffset 排序，查询 API 使用 Id 进行稳定分页。
        builder.HasIndex(x => x.Id);
    }
}
