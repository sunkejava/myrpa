using AgentRPA.Domain.Tasks;
using DomainExecution = AgentRPA.Domain.Execution.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentRPA.Infrastructure.Persistence.Configurations;

/// <summary>任务、任务项与执行实例数据库映射。</summary>
public sealed class TaskConfiguration : IEntityTypeConfiguration<RpaTask>, IEntityTypeConfiguration<TaskItem>, IEntityTypeConfiguration<DomainExecution>
{
    public void Configure(EntityTypeBuilder<RpaTask> b)
    {
        b.ToTable("tasks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.SubjectId);
    }

    void IEntityTypeConfiguration<TaskItem>.Configure(EntityTypeBuilder<TaskItem> b)
    {
        b.ToTable("task_items");
        b.HasKey(x => x.Id);
        b.Property(x => x.InputJson).IsRequired();
        b.HasIndex(x => new { x.TaskId, x.Sequence }).IsUnique();
        b.HasIndex(x => x.Status);
    }

    void IEntityTypeConfiguration<DomainExecution>.Configure(EntityTypeBuilder<DomainExecution> b)
    {
        b.ToTable("executions");
        b.HasKey(x => x.Id);
        b.Property(x => x.DispatchKey).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.DispatchKey).IsUnique();
        b.HasIndex(x => x.TaskItemId);
        b.HasIndex(x => x.NodeId);
        b.HasIndex(x => x.Status);
    }
}
