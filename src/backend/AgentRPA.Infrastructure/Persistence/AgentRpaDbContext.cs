using AgentRPA.Domain.Execution;
using AgentRPA.Domain.HumanIntervention;
using AgentRPA.Domain.Resources;
using AgentRPA.Domain.Tasks;
using AgentRPA.Domain.Workflow;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Persistence;

/// <summary>AgentRPA 数据库上下文，负责平台运行数据的持久化。</summary>
public sealed class AgentRpaDbContext(DbContextOptions<AgentRpaDbContext> options) : DbContext(options)
{
    public DbSet<City> Cities => Set<City>();
    public DbSet<BusinessSystem> BusinessSystems => Set<BusinessSystem>();
    public DbSet<BusinessFunction> BusinessFunctions => Set<BusinessFunction>();
    public DbSet<NodePool> NodePools => Set<NodePool>();
    public DbSet<ExecutionNode> ExecutionNodes => Set<ExecutionNode>();
    public DbSet<NodeCapability> NodeCapabilities => Set<NodeCapability>();
    public DbSet<WorkerSlot> WorkerSlots => Set<WorkerSlot>();
    public DbSet<NodeLease> NodeLeases => Set<NodeLease>();
    public DbSet<ResourceLock> ResourceLocks => Set<ResourceLock>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<RpaTask> Tasks => Set<RpaTask>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<Execution> Executions => Set<Execution>();
    public DbSet<HumanIntervention> HumanInterventions => Set<HumanIntervention>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentRpaDbContext).Assembly);
}
