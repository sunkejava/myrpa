using System.Collections.Concurrent;
using AgentRPA.Application.Nodes;
using AgentRPA.Contracts.Nodes;
using Microsoft.AspNetCore.SignalR;

namespace AgentRPA.Api.Hubs;

/// <summary>维护 NodeAgent 与 SignalR ConnectionId 的映射。</summary>
public sealed class NodeAgentConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, string> _connections = new();
    public void Bind(Guid nodeId, string connectionId) => _connections[nodeId] = connectionId;
    public void Remove(Guid nodeId, string connectionId) => _connections.TryRemove(new KeyValuePair<Guid, string>(nodeId, connectionId));
    public bool TryGet(Guid nodeId, out string? connectionId) => _connections.TryGetValue(nodeId, out connectionId);
}

/// <summary>Server 与 NodeAgent 的实时双向通信 Hub。</summary>
public sealed class NodeAgentHub(
    NodeAgentConnectionRegistry connections,
    INodeRegistryService nodeRegistry) : Hub<INodeAgentClient>
{
    public Task Connect(NodeAgentConnectRequest request)
    {
        connections.Bind(request.NodeId, Context.ConnectionId);
        Context.Items["NodeId"] = request.NodeId;
        return Task.CompletedTask;
    }

    /// <summary>NodeAgent 通过 SignalR 上报心跳，服务端返回统一 ACK。</summary>
    public async Task<NodeHeartbeatAck> Heartbeat(NodeHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var accepted = await nodeRegistry.HeartbeatAsync(
            request.NodeId,
            request.AgentVersion,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!accepted)
            throw new HubException("Node 不存在或已被禁用。");

        connections.Bind(request.NodeId, Context.ConnectionId);
        Context.Items["NodeId"] = request.NodeId;
        return new NodeHeartbeatAck(request.NodeId, DateTimeOffset.UtcNow, request.Status);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("NodeId", out var value) && value is Guid nodeId)
            connections.Remove(nodeId, Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
