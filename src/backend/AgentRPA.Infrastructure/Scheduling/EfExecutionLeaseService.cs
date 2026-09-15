using AgentRPA.Application.Scheduling;
using AgentRPA.Domain.Execution;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Scheduling;

/// <summary>基于 EF Core 的执行租约服务。当前以进程级互斥配合数据库持久化保证单实例竞争安全。</summary>
public sealed class EfExecutionLeaseService(AgentRpaDbContext db) : IExecutionLeaseService
{
    private static readonly SemaphoreSlim AcquireGate = new(1, 1);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    public async Task<ExecutionAssignment?> TryAcquireAsync(
        ExecutionNodeSnapshot node,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        await AcquireGate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            await RecoverExpiredAsync(node.NodeId, now, cancellationToken);

            var slot = await db.WorkerSlots
                .Where(x => x.NodeId == node.NodeId && x.Enabled &&
                            (x.ExecutionId == null || x.LeaseExpiresAt <= now))
                .OrderBy(x => x.SlotName)
                .FirstOrDefaultAsync(cancellationToken);

            if (slot is null) return null;

            var expiresAt = now.Add(LeaseDuration);
            slot.Acquire(executionId, expiresAt);
            var lease = new NodeLease(executionId, node.NodeId, slot.Id, expiresAt);
            db.Set<NodeLease>().Add(lease);
            await db.SaveChangesAsync(cancellationToken);

            return new ExecutionAssignment(node.NodeId, slot.Id, lease.Id, 0);
        }
        finally
        {
            AcquireGate.Release();
        }
    }

    public async Task<bool> RenewAsync(Guid leaseId, Guid executionId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var lease = await db.Set<NodeLease>()
            .SingleOrDefaultAsync(x => x.Id == leaseId && !x.Released, cancellationToken);

        if (lease is null || lease.ExecutionId != executionId || lease.ExpiresAt <= now)
            return false;

        var slot = await db.WorkerSlots
            .SingleOrDefaultAsync(x => x.Id == lease.WorkerSlotId, cancellationToken);

        if (slot is null || slot.ExecutionId != executionId || !slot.Enabled)
            return false;

        var expiresAt = now.Add(LeaseDuration);
        lease.Renew(expiresAt, now);
        slot.Acquire(executionId, expiresAt);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReleaseAsync(Guid leaseId, CancellationToken cancellationToken)
    {
        var lease = await db.Set<NodeLease>().SingleOrDefaultAsync(x => x.Id == leaseId, cancellationToken);
        if (lease is null) return;

        var slot = await db.WorkerSlots.SingleOrDefaultAsync(x => x.Id == lease.WorkerSlotId, cancellationToken);
        if (slot is not null && slot.ExecutionId == lease.ExecutionId)
            slot.Release();

        lease.Release();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecoverExpiredAsync(Guid nodeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expiredLeases = await db.Set<NodeLease>()
            .Where(x => x.NodeId == nodeId && !x.Released && x.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expiredLeases.Count == 0) return;

        foreach (var lease in expiredLeases)
        {
            var slot = await db.WorkerSlots.SingleOrDefaultAsync(x => x.Id == lease.WorkerSlotId, cancellationToken);
            if (slot is not null && slot.ExecutionId == lease.ExecutionId)
                slot.Release();
            lease.Release();
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
