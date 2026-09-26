using AgentRPA.Contracts.Nodes;
using AgentRPA.NodeAgent.Execution;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

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
    private TimeSpan? lastCpuTime;
    private long lastCpuSample;

    /// <summary>以心跳间隔内的进程 CPU 时间计算占用率，内存为进程常驻内存 MiB。</summary>
    private (double CpuPercent, double MemoryMiB) ReadProcessMetrics()
    {
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var sample = Stopwatch.GetTimestamp();
        var elapsed = lastCpuSample == 0 ? 0 : Stopwatch.GetElapsedTime(lastCpuSample, sample).TotalSeconds;
        var percent = lastCpuTime.HasValue && elapsed > 0
            ? Math.Clamp((cpu - lastCpuTime.Value).TotalSeconds / elapsed / Environment.ProcessorCount * 100, 0, 100) : 0;
        lastCpuTime = cpu;
        lastCpuSample = sample;
        return (Math.Round(percent, 2), Math.Round(process.WorkingSet64 / 1048576d, 2));
    }

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
                    var metrics = ReadProcessMetrics();
                    await connection.InvokeAsync<NodeHeartbeatAck>("Heartbeat", new NodeHeartbeatRequest(registration.NodeId, config.AgentVersion, "Online", metrics.CpuPercent, metrics.MemoryMiB, Math.Max(0, config.WorkerSlots.Count - executions.Count), DateTimeOffset.UtcNow), stoppingToken);
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
        var extracted = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            await ReportAsync(connection, command, "Running", null, 0, "NodeAgent 开始执行 Workflow", linked.Token);
            await runtime.ExecuteAsync(command.WorkflowPayload, command.Parameters, async e =>
            {
                if (e.OutputKey is not null && e.OutputValue is not null) extracted[e.OutputKey] = e.OutputValue;
                if (e.Artifact is not null)
                    await UploadArtifactAsync(command, e.Artifact, linked.Token);
                await ReportAsync(connection, command, e.Status, e.StepId, e.ProgressPercent, e.Message, linked.Token, e.StepType,
                    e.Status == "Succeeded" ? JsonSerializer.Serialize(extracted) : null, e.InterventionType, e.InterventionTitle);
                if (string.Equals(e.Status, "WaitingForHuman", StringComparison.OrdinalIgnoreCase))
                {
                    if (!humanResumes.TryGetValue(command.ExecutionId, out var pending))
                        throw new InvalidOperationException("人工介入等待状态丢失。");
                    await pending.Task.WaitAsync(linked.Token);
                    // 每个 HumanTask 都要单独确认，不能沿用上一节点已经完成的信号。
                    humanResumes[command.ExecutionId] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }, linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { await ReportAsync(connection, command, "Cancelled", null, null, "执行已取消", CancellationToken.None); }
        catch (Exception ex) { logger.LogError(ex, "Execution {ExecutionId} failed", command.ExecutionId); await ReportAsync(connection, command, "Failed", null, null, ex.Message, CancellationToken.None); }
        finally { humanResumes.TryRemove(command.ExecutionId, out _); if (executions.TryRemove(command.ExecutionId, out var source)) source.Dispose(); }
    }

    private Task CancelExecutionAsync(Guid executionId) { if (executions.TryGetValue(executionId, out var source)) source.Cancel(); if (humanResumes.TryGetValue(executionId, out var resume)) resume.TrySetCanceled(); return Task.CompletedTask; }
    private Task ResumeExecutionAsync(Guid executionId) { if (humanResumes.TryGetValue(executionId, out var resume)) resume.TrySetResult(true); return Task.CompletedTask; }
    private static Task ReportAsync(HubConnection connection, ExecutionCommand command, string status, string? stepId, int? percent, string? message, CancellationToken cancellationToken, string? stepType = null, string? resultJson = null, string? interventionType = null, string? interventionTitle = null) => connection.InvokeAsync("ReportProgress", new ExecutionProgress(command.ExecutionId, command.NodeId, command.WorkerSlotId, status, stepId, percent, message, DateTimeOffset.UtcNow, stepType, resultJson, interventionType, interventionTitle), cancellationToken);

    private async Task UploadArtifactAsync(ExecutionCommand command, WorkflowRuntimeArtifact artifact, CancellationToken cancellationToken)
    {
        if (artifact.Size > 50L * 1024 * 1024) throw new InvalidOperationException("产物超过 50 MiB 上传限制。");
        var client = httpClientFactory.CreateClient("AgentRPA.Server");
        var url = $"api/nodes/{command.NodeId}/executions/{command.ExecutionId}/artifacts?workerSlotId={command.WorkerSlotId}&artifactType={Uri.EscapeDataString(artifact.ArtifactType)}&fileName={Uri.EscapeDataString(artifact.FileName)}&sha256={artifact.Hash}";
        await using var file = File.OpenRead(artifact.StorageKey);
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StreamContent(file) };
        request.Content.Headers.ContentLength = file.Length;
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(artifact.ContentType ?? "application/octet-stream");
        request.Headers.Add("X-Agent-Key", options.Value.AgentKey);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

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
