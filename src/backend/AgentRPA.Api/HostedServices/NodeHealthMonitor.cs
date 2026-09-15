using AgentRPA.Application.Nodes;

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
}
