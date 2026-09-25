using AgentRPA.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

public sealed class TaskApprovalConfiguration : IEntityTypeConfiguration<TaskApproval>
{
    public void Configure(EntityTypeBuilder<TaskApproval> b)
    {
        b.ToTable("task_approvals");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.TaskId).IsUnique();
        b.HasIndex(x => x.Status);
        b.Property(x => x.Status).IsConcurrencyToken();
        b.Property(x => x.Reason).HasMaxLength(1000);
    }
}
