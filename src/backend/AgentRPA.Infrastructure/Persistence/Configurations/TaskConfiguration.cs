using AgentRPA.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace AgentRPA.Infrastructure.Persistence.Configurations;
public sealed class TaskConfiguration : IEntityTypeConfiguration<RpaTask>, IEntityTypeConfiguration<TaskItem>, IEntityTypeConfiguration<Execution>
{
    public void Configure(EntityTypeBuilder<RpaTask> b) { b.ToTable("tasks"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.HasIndex(x => x.Status); b.HasIndex(x => x.SubjectId); }
    void IEntityTypeConfiguration<TaskItem>.Configure(EntityTypeBuilder<TaskItem> b) { b.ToTable("task_items"); b.HasKey(x => x.Id); b.Property(x => x.InputJson).IsRequired(); b.HasIndex(x => new { x.TaskId, x.Sequence }).IsUnique(); b.HasIndex(x => x.Status); }
    void IEntityTypeConfiguration<Execution>.Configure(EntityTypeBuilder<Execution> b) { b.ToTable("executions"); b.HasKey(x => x.Id); b.HasIndex(x => x.TaskItemId); b.HasIndex(x => x.NodeId); b.HasIndex(x => x.Status); }
}
