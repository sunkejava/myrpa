using AgentRPA.Application.Scheduling;
using AgentRPA.Domain.Execution;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Scheduling;

/// <summary>基于数据库事务和乐观并发的执行租约服务，同时原子占用 UKey/智能卡等硬件资源。</summary>
public sealed class EfExecutionLeaseService(AgentRpaDbContext db) : IExecutionLeaseService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    public async Task<ExecutionAssignment?> TryAcquireAsync(ExecutionNodeSnapshot node, Guid executionId, IReadOnlySet<string>? requiredHardwareIds, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            await RecoverExpiredAsync(node.NodeId, now, cancellationToken);
            await RecoverExpiredResourceLocksAsync(now, cancellationToken);

            var slot = await db.WorkerSlots
                .Where(x => x.NodeId == node.NodeId && x.Enabled && (x.ExecutionId == null || x.LeaseExpiresAt <= now))
                .OrderBy(x => x.SlotName)
                .FirstOrDefaultAsync(cancellationToken);
            if (slot is null) { await transaction.RollbackAsync(cancellationToken); return null; }

            var hardwareIds = (requiredHardwareIds ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (hardwareIds.Length > 0)
            {
                var busyHardware = await db.ResourceLocks.AsNoTracking()
                    .Where(x => !x.Released && x.ExpiresAt > now && x.ResourceType == "UKey" && hardwareIds.Contains(x.ResourceId))
                    .Select(x => x.ResourceId)
                    .ToListAsync(cancellationToken);
                if (busyHardware.Count > 0) { await transaction.RollbackAsync(cancellationToken); return null; }
            }

            var expiresAt = now.Add(LeaseDuration);
            slot.Acquire(executionId, expiresAt);
            db.Set<NodeLease>().Add(new NodeLease(executionId, node.NodeId, slot.Id, expiresAt));
            foreach (var hardwareId in hardwareIds)
                db.ResourceLocks.Add(new ResourceLock("UKey", hardwareId, executionId, expiresAt));

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var lease = await db.NodeLeases.AsNoTracking().Where(x => x.ExecutionId == executionId && x.WorkerSlotId == slot.Id).OrderByDescending(x => x.Id).FirstAsync(cancellationToken);
            return new ExecutionAssignment(node.NodeId, slot.Id, lease.Id, 0);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            // 唯一索引可将并发硬件抢占收敛为“本次无资源”。
            return null;
        }
    }

    public async Task<bool> RenewAsync(Guid leaseId, Guid executionId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var lease = await db.Set<NodeLease>().SingleOrDefaultAsync(x => x.Id == leaseId && !x.Released, cancellationToken);
        if (lease is null || lease.ExecutionId != executionId || lease.ExpiresAt <= now) return false;
        var slot = await db.WorkerSlots.SingleOrDefaultAsync(x => x.Id == lease.WorkerSlotId, cancellationToken);
        if (slot is null || slot.ExecutionId != executionId || !slot.Enabled) return false;
        var expiresAt = now.Add(LeaseDuration);
        lease.Renew(expiresAt, now); slot.Acquire(executionId, expiresAt);
        var locks = await db.ResourceLocks.Where(x => x.ExecutionId == executionId && !x.Released).ToListAsync(cancellationToken);
        foreach (var resourceLock in locks) resourceLock.Renew(expiresAt, now);
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    public async Task ReleaseAsync(Guid leaseId, CancellationToken cancellationToken)
    {
        var lease = await db.Set<NodeLease>().SingleOrDefaultAsync(x => x.Id == leaseId, cancellationToken);
        if (lease is null) return;
        var slot = await db.WorkerSlots.SingleOrDefaultAsync(x => x.Id == lease.WorkerSlotId, cancellationToken);
        if (slot is not null && slot.ExecutionId == lease.ExecutionId) slot.Release();
        lease.Release();
        foreach (var resourceLock in await db.ResourceLocks.Where(x => x.ExecutionId == lease.ExecutionId && !x.Released).ToListAsync(cancellationToken)) resourceLock.Release();
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
    }

    private async Task RecoverExpiredAsync(Guid nodeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expiredLeases = await db.Set<NodeLease>().Where(x => x.NodeId == nodeId && !x.Released && x.ExpiresAt <= now).ToListAsync(cancellationToken);
        foreach (var lease in expiredLeases)
        {
            var slot = await db.WorkerSlots.SingleOrDefaultAsync(x => x.Id == lease.WorkerSlotId, cancellationToken);
            if (slot is not null && slot.ExecutionId == lease.ExecutionId) slot.Release();
            lease.Release();
            foreach (var resourceLock in await db.ResourceLocks.Where(x => x.ExecutionId == lease.ExecutionId && !x.Released).ToListAsync(cancellationToken)) resourceLock.Release();
        }
        if (expiredLeases.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecoverExpiredResourceLocksAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expired = await db.ResourceLocks.Where(x => !x.Released && x.ExpiresAt <= now).ToListAsync(cancellationToken);
        foreach (var resourceLock in expired) resourceLock.Release();
        if (expired.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }
}
