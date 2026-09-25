using AgentRPA.Application.Nodes;
using AgentRPA.Domain.Execution;
using AgentRPA.Domain.Tasks;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainTaskStatus = AgentRPA.Domain.Tasks.TaskStatus;

namespace AgentRPA.Api.HostedServices;

/// <summary>周期检查 Node Agent 心跳，将长期无心跳节点标记为 Offline，避免调度继续使用失联节点。</summary>
public sealed class NodeHealthMonitor(
    IServiceScopeFactory scopeFactory,
    ILogger<NodeHealthMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(45);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var registry = scope.ServiceProvider.GetRequiredService<INodeRegistryService>();
                var count = await registry.MarkOfflineNodesAsync(HeartbeatTimeout, stoppingToken);
                if (count > 0)
                    logger.LogWarning("Marked {Count} stale execution nodes as Offline.", count);
                var db = scope.ServiceProvider.GetRequiredService<AgentRpaDbContext>();
                await FailExpiredOfflineExecutionsAsync(db, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Node health monitor failed.");
            }
        }
    }

    private async Task FailExpiredOfflineExecutionsAsync(AgentRpaDbContext db, CancellationToken ct)
    {
        // SQLite does not translate DateTimeOffset ordering: narrow the set in SQL, then check
        // lease expiry in .NET. An offline node with an unexpired lease can still reconnect.
        var candidates = await (from lease in db.NodeLeases.AsNoTracking()
            join node in db.ExecutionNodes.AsNoTracking() on lease.NodeId equals node.Id
            join execution in db.Executions.AsNoTracking() on lease.ExecutionId equals execution.Id
            where node.Status == NodeStatus.Offline &&
                (execution.Status == ExecutionStatus.Dispatched || execution.Status == ExecutionStatus.Running ||
                 execution.Status == ExecutionStatus.Paused || execution.Status == ExecutionStatus.WaitingForHuman)
            select new { execution.Id, lease.ExpiresAt }).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var candidate in candidates.Where(x => x.ExpiresAt <= now).DistinctBy(x => x.Id))
        {
            // A transaction serializes recovery with the node's terminal progress report.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var execution = await db.Executions.SingleAsync(x => x.Id == candidate.Id, ct);
            var node = execution.NodeId.HasValue
                ? await db.ExecutionNodes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == execution.NodeId.Value, ct) : null;
            var leases = await db.NodeLeases.Where(x => x.ExecutionId == candidate.Id).ToListAsync(ct);
            var lease = leases.OrderByDescending(x => x.ExpiresAt).FirstOrDefault();
            if (node?.Status != NodeStatus.Offline || lease is null || lease.ExpiresAt > DateTimeOffset.UtcNow ||
                execution.Status is not (ExecutionStatus.Dispatched or ExecutionStatus.Running or ExecutionStatus.Paused or ExecutionStatus.WaitingForHuman))
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                continue;
            }
            const string message = "节点离线且租约过期，外部业务状态未确认；禁止自动重试，请先人工核验。";
            execution.SetStatus(ExecutionStatus.Failed, message);
            var item = await db.TaskItems.SingleAsync(x => x.Id == execution.TaskItemId, ct);
            var task = await db.Tasks.SingleAsync(x => x.Id == item.TaskId, ct);
            item.Fail(message);
            task.SetStatus(DomainTaskStatus.Failed);
            var sequence = (await db.ExecutionLogs.Where(x => x.ExecutionId == execution.Id)
                .Select(x => (long?)x.Sequence).MaxAsync(ct) ?? -1) + 1;
            db.ExecutionLogs.Add(new ExecutionLog(execution.Id, sequence, ExecutionLogLevel.Warning,
                ExecutionLogEventType.System, message));
            foreach (var current in await db.NodeLeases.Where(x => x.ExecutionId == execution.Id && !x.Released).ToListAsync(ct))
                current.Release();
            var slot = await db.WorkerSlots.SingleOrDefaultAsync(x => x.Id == lease.WorkerSlotId, ct);
            if (slot is not null && slot.ExecutionId == execution.Id) slot.Release();
            foreach (var resource in await db.ResourceLocks.Where(x => x.ExecutionId == execution.Id && !x.Released).ToListAsync(ct))
                resource.Release();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogWarning("Execution {ExecutionId} moved to manual reconciliation after offline lease expiry", execution.Id);
            db.ChangeTracker.Clear();
        }
    }
}
