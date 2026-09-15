using AgentRPA.Domain.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace AgentRPA.Infrastructure.Persistence.Configurations;
public sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>, IEntityTypeConfiguration<WorkflowVersion>, IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<Workflow> b) { b.ToTable("workflows"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.HasIndex(x => x.BusinessFunctionId); }
    void IEntityTypeConfiguration<WorkflowVersion>.Configure(EntityTypeBuilder<WorkflowVersion> b) { b.ToTable("workflow_versions"); b.HasKey(x => x.Id); b.Property(x => x.DefinitionJson).IsRequired(); b.HasIndex(x => new { x.WorkflowId, x.Version }).IsUnique(); }
    void IEntityTypeConfiguration<WorkflowStep>.Configure(EntityTypeBuilder<WorkflowStep> b) { b.ToTable("workflow_steps"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.Property(x => x.ConfigJson).IsRequired(); b.HasIndex(x => new { x.WorkflowVersionId, x.Order }).IsUnique(); }
}
