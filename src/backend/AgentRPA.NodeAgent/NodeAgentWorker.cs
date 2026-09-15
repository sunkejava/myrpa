using System.Net.Http.Json;
using AgentRPA.Contracts.Nodes;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

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

/// <summary>NodeAgent 的通信生命周期：注册 → SignalR 连接 → 心跳 → 接收命令。</summary>
public sealed class NodeAgentWorker(
    IHttpClientFactory httpClientFactory,
    IOptions<NodeAgentOptions> options,
    ILogger<NodeAgentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        var registration = await RegisterAsync(config, stoppingToken);
        if (registration is null) return;
        var hubUrl = $"{config.ServerUrl.TrimEnd('/')}/hubs/node-agent";
        await using var connection = new HubConnectionBuilder().WithUrl(hubUrl).WithAutomaticReconnect().WithStatefulReconnect().Build();

        connection.On<ExecutionCommand>("ExecuteAsync", command => { logger.LogInformation("Received execution command {ExecutionId} for slot {WorkerSlotId}", command.ExecutionId, command.WorkerSlotId); return Task.CompletedTask; });
        connection.On<Guid>("CancelAsync", id => { logger.LogInformation("Cancel requested for {ExecutionId}", id); return Task.CompletedTask; });
        connection.On<Guid>("PauseAsync", id => { logger.LogInformation("Pause requested for {ExecutionId}", id); return Task.CompletedTask; });
        connection.On<Guid>("ResumeAsync", id => { logger.LogInformation("Resume requested for {ExecutionId}", id); return Task.CompletedTask; });

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (connection.State == HubConnectionState.Disconnected)
                    await connection.StartAsync(stoppingToken);
                if (connection.State == HubConnectionState.Connected)
                    await connection.InvokeAsync("Connect", new NodeAgentConnectRequest(registration.NodeId, config.AgentKey, config.AgentVersion), stoppingToken);
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
