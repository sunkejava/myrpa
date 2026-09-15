using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Node Agent 是部署在 Windows VM、Windows 物理机或其他执行节点上的独立执行平面。
// 后续在这里注册 NodeRegistryClient、SignalR 通信、WorkerSlot 管理和本机 Provider。
builder.Services.AddHostedService<NodeAgentWorker>();

await builder.Build().RunAsync();

internal sealed class NodeAgentWorker(ILogger<NodeAgentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AgentRPA Node Agent started at {Time}", DateTimeOffset.UtcNow);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Phase 1 将实现：注册、心跳、能力上报、接收 ExecutionCommand。
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
