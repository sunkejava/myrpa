using AgentRPA.Domain.Execution;
using AgentRPA.Domain.Resources;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentRpaDbContext).Assembly);
    }
}
