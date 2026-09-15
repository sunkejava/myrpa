using AgentRPA.Contracts.Nodes;
using AgentRPA.NodeAgent.Execution;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace AgentRPA.NodeAgent;

public sealed class NodeAgentOptions
{
    public string ServerUrl { get; set; } = "https://localhost:5001";
    public string AgentKey { get; set; } = Environment.MachineName;
    public string Name { get; set; } = Environment.MachineName;
    public string NodeKind { get; set; } = "Physical";
    public string OsPlatform { get; set; } = OperatingSystem.IsWindows() ? "Windows" : "Linux";
    public string Architecture { get; set; } = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
    public string AgentVersion { get; set; } = "0.1.0";
    public string? NetworkZone { get; set; }
    public Guid? NodePoolId { get; set; }
    public List<NodeCapabilityDto> Capabilities { get; set; } = [new("DesktopUI"), new("Browser:Edge")];
    public List<string> WorkerSlots { get; set; } = ["worker-01"];
}

/// <summary>NodeAgent：注册、实时心跳、命令接收及本地 Workflow 执行。</summary>
public sealed class NodeAgentWorker(
    IHttpClientFactory httpClientFactory,
    IOptions<NodeAgentOptions> options,
    IWorkflowRuntime runtime,
    ILogger<NodeAgentWorker> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> executions = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        var registration = await RegisterAsync(config, stoppingToken);
        if (registration is null) return;

        var hubUrl = $"{config.ServerUrl.TrimEnd('/')}/hubs/node-agent";
        await using var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .WithStatefulReconnect()
            .Build();

        connection.On<ExecutionCommand>("ExecuteAsync", command => StartExecutionAsync(connection, command, stoppingToken));
        connection.On<Guid>("CancelAsync", id => CancelExecutionAsync(id));
        connection.On<Guid>("PauseAsync", id => Task.CompletedTask);
        connection.On<Guid>("ResumeAsync", id => Task.CompletedTask);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (connection.State == HubConnectionState.Disconnected)
                    await connection.StartAsync(stoppingToken);
                if (connection.State == HubConnectionState.Connected)
                {
                    await connection.InvokeAsync("Connect", new NodeAgentConnectRequest(registration.NodeId, config.AgentKey, config.AgentVersion), stoppingToken);
                    await connection.InvokeAsync<NodeHeartbeatAck>("Heartbeat", new NodeHeartbeatRequest(
                        registration.NodeId, config.AgentVersion, "Online", 0, 0, Math.Max(0, config.WorkerSlots.Count - executions.Count), DateTimeOffset.UtcNow), stoppingToken);
                }
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "NodeAgent communication failed; retrying");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task StartExecutionAsync(HubConnection connection, ExecutionCommand command, CancellationToken stoppingToken)
    {
        if (!executions.TryAdd(command.ExecutionId, CancellationTokenSource.CreateLinkedTokenSource(stoppingToken)))
            return;
        var linked = executions[command.ExecutionId];
        try
        {
            await ReportAsync(connection, command, "Running", null, 0, "NodeAgent 开始执行 Workflow", linked.Token);
            await runtime.ExecuteAsync(command.WorkflowPayload, command.Parameters, e =>
                ReportAsync(connection, command, e.Status, e.StepId, e.ProgressPercent, e.Message, linked.Token), linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            await ReportAsync(connection, command, "Cancelled", null, null, "执行已取消", CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Execution {ExecutionId} failed", command.ExecutionId);
            await ReportAsync(connection, command, "Failed", null, null, ex.Message, CancellationToken.None);
        }
        finally
        {
            if (executions.TryRemove(command.ExecutionId, out var source)) source.Dispose();
        }
    }

    private Task CancelExecutionAsync(Guid executionId)
    {
        if (executions.TryGetValue(executionId, out var source)) source.Cancel();
        return Task.CompletedTask;
    }

    private static Task ReportAsync(HubConnection connection, ExecutionCommand command, string status, string? stepId, int? percent, string? message, CancellationToken cancellationToken)
        => connection.InvokeAsync("ReportProgress", new ExecutionProgress(command.ExecutionId, command.NodeId, command.WorkerSlotId, status, stepId, percent, message, DateTimeOffset.UtcNow), cancellationToken);

    private async Task<NodeRegistrationResponse?> RegisterAsync(NodeAgentOptions config, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("AgentRPA.Server");
            var request = new RegisterNodeRequest(config.AgentKey, config.Name, config.NodeKind, config.OsPlatform, config.Architecture,
                config.AgentVersion, config.NetworkZone, config.NodePoolId, config.Capabilities, config.WorkerSlots);
            using var response = await client.PostAsJsonAsync("api/nodes/register", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<NodeRegistrationResponse>(cancellationToken: cancellationToken);
            logger.LogInformation("Node registered: {NodeId}, status={Status}", result?.NodeId, result?.Status);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NodeAgent registration failed");
            return null;
        }
    }
}
