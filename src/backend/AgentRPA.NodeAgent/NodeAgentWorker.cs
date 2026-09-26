using AgentRPA.Contracts.Nodes;
using AgentRPA.NodeAgent.Execution;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace AgentRPA.NodeAgent;

public sealed class NodeAgentOptions
{
    public string ServerUrl { get; set; } = "https://localhost:5001";
    public string AgentKey { get; set; } = Environment.MachineName;
    public string RegistrationKey { get; set; } = string.Empty;
    public string Name { get; set; } = Environment.MachineName;
    public string NodeKind { get; set; } = "Physical";
    public string OsPlatform { get; set; } = OperatingSystem.IsWindows() ? "Windows" : "Linux";
    public string Architecture { get; set; } = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
    public string AgentVersion { get; set; } = "0.1.0";
    public string? NetworkZone { get; set; }
    public Guid? NodePoolId { get; set; }
    public List<NodeCapabilityDto> Capabilities { get; set; } = [new("DesktopUI"), new("Browser:Edge"), new("Adapter:qd-social-security")];
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
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>> humanResumes = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        NodeRegistrationResponse? registration = null;
        while (registration is null && !stoppingToken.IsCancellationRequested)
        {
            registration = await RegisterAsync(config, stoppingToken);
            if (registration is null) await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        if (registration is null) return;
        if (registration.Status is "PendingApproval" or "Rejected")
        {
            logger.LogWarning("Node {NodeId} is pending administrator approval; waiting before connecting for execution.", registration.NodeId);
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                registration = await RegisterAsync(config, stoppingToken);
                if (registration is null) continue;
                if (registration.Status is not ("PendingApproval" or "Rejected")) break;
            }
        }

        var hubUrl = $"{config.ServerUrl.TrimEnd('/')}/hubs/node-agent";
        await using var connection = new HubConnectionBuilder().WithUrl(hubUrl).WithAutomaticReconnect().WithStatefulReconnect().Build();
        connection.On<ExecutionCommand>("ExecuteAsync", command =>
        {
            // SignalR client handlers are serialized. Waiting for the entire workflow here would
            // block CancelAsync / ResumeAsync while the node waits for a human or a long step.
            // StartExecutionAsync registers its cancellation source before its first await.
            _ = RunExecutionAsync(connection, command, stoppingToken);
            return Task.CompletedTask;
        });
        connection.On<Guid>("CancelAsync", CancelExecutionAsync);
        connection.On<Guid>("PauseAsync", _ => Task.CompletedTask);
        connection.On<Guid>("ResumeAsync", ResumeExecutionAsync);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (connection.State == HubConnectionState.Disconnected) await connection.StartAsync(stoppingToken);
                if (connection.State == HubConnectionState.Connected)
                {
                    await connection.InvokeAsync("Connect", new NodeAgentConnectRequest(registration.NodeId, config.AgentKey, config.AgentVersion), stoppingToken);
                    await connection.InvokeAsync<NodeHeartbeatAck>("Heartbeat", new NodeHeartbeatRequest(registration.NodeId, config.AgentVersion, "Online", 0, 0, Math.Max(0, config.WorkerSlots.Count - executions.Count), DateTimeOffset.UtcNow), stoppingToken);
                }
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "NodeAgent communication failed; retrying"); await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        }
    }

    private async Task RunExecutionAsync(HubConnection connection, ExecutionCommand command, CancellationToken stoppingToken)
    {
        try { await StartExecutionAsync(connection, command, stoppingToken); }
        catch (Exception ex) { logger.LogError(ex, "Execution {ExecutionId} status report failed", command.ExecutionId); }
    }

    private async Task StartExecutionAsync(HubConnection connection, ExecutionCommand command, CancellationToken stoppingToken)
    {
        if (!executions.TryAdd(command.ExecutionId, CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))) return;
        var linked = executions[command.ExecutionId];
        var resumeSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        humanResumes[command.ExecutionId] = resumeSignal;
        try
        {
            await ReportAsync(connection, command, "Running", null, 0, "NodeAgent 开始执行 Workflow", linked.Token);
            await runtime.ExecuteAsync(command.WorkflowPayload, command.Parameters, async e =>
            {
                await ReportAsync(connection, command, e.Status, e.StepId, e.ProgressPercent, e.Message, linked.Token, e.StepType);
                if (e.Artifact is not null)
                    await connection.InvokeAsync("ReportArtifact", new ExecutionArtifactReport(command.ExecutionId, command.NodeId, command.WorkerSlotId, e.Artifact.ArtifactType, e.Artifact.FileName, e.Artifact.StorageKey, e.Artifact.ContentType, e.Artifact.Size, e.Artifact.Hash, DateTimeOffset.UtcNow), linked.Token);
                if (string.Equals(e.Status, "WaitingForHuman", StringComparison.OrdinalIgnoreCase)) await resumeSignal.Task.WaitAsync(linked.Token);
            }, linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { await ReportAsync(connection, command, "Cancelled", null, null, "执行已取消", CancellationToken.None); }
        catch (Exception ex) { logger.LogError(ex, "Execution {ExecutionId} failed", command.ExecutionId); await ReportAsync(connection, command, "Failed", null, null, ex.Message, CancellationToken.None); }
        finally { humanResumes.TryRemove(command.ExecutionId, out _); if (executions.TryRemove(command.ExecutionId, out var source)) source.Dispose(); }
    }

    private Task CancelExecutionAsync(Guid executionId) { if (executions.TryGetValue(executionId, out var source)) source.Cancel(); if (humanResumes.TryGetValue(executionId, out var resume)) resume.TrySetCanceled(); return Task.CompletedTask; }
    private Task ResumeExecutionAsync(Guid executionId) { if (humanResumes.TryGetValue(executionId, out var resume)) resume.TrySetResult(true); return Task.CompletedTask; }
    private static Task ReportAsync(HubConnection connection, ExecutionCommand command, string status, string? stepId, int? percent, string? message, CancellationToken cancellationToken, string? stepType = null) => connection.InvokeAsync("ReportProgress", new ExecutionProgress(command.ExecutionId, command.NodeId, command.WorkerSlotId, status, stepId, percent, message, DateTimeOffset.UtcNow, stepType), cancellationToken);

    private async Task<NodeRegistrationResponse?> RegisterAsync(NodeAgentOptions config, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(config.RegistrationKey)) throw new InvalidOperationException("NodeAgent:RegistrationKey 未配置，拒绝进行节点注册。");
            var client = httpClientFactory.CreateClient("AgentRPA.Server");
            var request = new RegisterNodeRequest(config.AgentKey, config.Name, config.NodeKind, config.OsPlatform, config.Architecture, config.AgentVersion, config.NetworkZone, config.NodePoolId, config.Capabilities, config.WorkerSlots);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/nodes/register") { Content = JsonContent.Create(request) };
            httpRequest.Headers.Add("X-Node-Registration-Key", config.RegistrationKey);
            using var response = await client.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<NodeRegistrationResponse>(cancellationToken: cancellationToken);
            logger.LogInformation("Node registered: {NodeId}, status={Status}", result?.NodeId, result?.Status);
            return result;
        }
        catch (Exception ex) { logger.LogError(ex, "NodeAgent registration failed"); return null; }
    }
}
