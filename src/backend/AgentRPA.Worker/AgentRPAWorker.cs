using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentRPA.Worker;

/// <summary>AgentRPA 后台任务宿主。当前负责保持 Worker 进程生命周期，后续承载任务调度执行循环。</summary>
public sealed class AgentRPAWorker(ILogger<AgentRPAWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AgentRPA Worker started.");
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 正常停止，不记录为异常。
        }
        finally
        {
            logger.LogInformation("AgentRPA Worker stopped.");
        }
    }
}
