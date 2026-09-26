using AgentRPA.Application.Nodes;
using AgentRPA.Domain.Execution;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Infrastructure.Nodes;

/// <summary>基于 EF Core 的节点注册表实现。</summary>
public sealed class EfNodeRegistryService(AgentRpaDbContext db) : INodeRegistryService
{
    public async Task<ExecutionNode> RegisterAsync(NodeRegistration registration, CancellationToken cancellationToken)
    {
        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.AgentKey == registration.AgentKey, cancellationToken);
        if (node is null)
        {
            node = new ExecutionNode(registration.AgentKey, registration.Name, registration.NodeKind, registration.OsPlatform, registration.Architecture);
            db.ExecutionNodes.Add(node);
        }
        else if (node.Status == NodeStatus.Revoked)
            throw new InvalidOperationException("该节点身份已吊销，必须更换 AgentKey 重新申请。");
        else if (node.Status == NodeStatus.Rejected)
            node.SetStatus(NodeStatus.PendingApproval);
        node.SetPool(registration.NodePoolId);
        node.SetNetworkZone(registration.NetworkZone);
        node.RegisterHeartbeat(registration.AgentVersion, DateTimeOffset.UtcNow);
        await ReplaceCapabilitiesInternalAsync(node.Id, registration.Capabilities, cancellationToken);
        await ReplaceSlotsInternalAsync(node.Id, registration.WorkerSlots, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return node;
    }

    public async Task<bool> HeartbeatAsync(Guid nodeId, string agentVersion, DateTimeOffset heartbeatAt, CancellationToken cancellationToken)
    {
        var node = await db.ExecutionNodes.SingleOrDefaultAsync(x => x.Id == nodeId, cancellationToken);
        if (node is null || node.Status is NodeStatus.PendingApproval or NodeStatus.Rejected or NodeStatus.Revoked or NodeStatus.Disabled) return false;
        node.RegisterHeartbeat(agentVersion, heartbeatAt);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RefreshCapabilitiesAsync(Guid nodeId, IReadOnlyCollection<NodeCapabilityInput> capabilities, CancellationToken cancellationToken)
    {
        if (!await db.ExecutionNodes.AnyAsync(x => x.Id == nodeId, cancellationToken)) throw new KeyNotFoundException($"Execution node {nodeId} not found.");
        await ReplaceCapabilitiesInternalAsync(nodeId, capabilities, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RefreshWorkerSlotsAsync(Guid nodeId, IReadOnlyCollection<string> slotNames, CancellationToken cancellationToken)
    {
        if (!await db.ExecutionNodes.AnyAsync(x => x.Id == nodeId, cancellationToken)) throw new KeyNotFoundException($"Execution node {nodeId} not found.");
        await ReplaceSlotsInternalAsync(nodeId, slotNames, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> MarkOfflineNodesAsync(TimeSpan heartbeatTimeout, CancellationToken cancellationToken)
    {
        var timeout = DateTimeOffset.UtcNow.Subtract(heartbeatTimeout);
        var onlineNodes = await db.ExecutionNodes.Where(x => x.Status == NodeStatus.Online).ToListAsync(cancellationToken);
        var staleNodes = onlineNodes.Where(x => x.LastHeartbeatAt == null || x.LastHeartbeatAt < timeout).ToList();

        foreach (var node in staleNodes)
            node.SetStatus(NodeStatus.Offline);

        if (staleNodes.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        return staleNodes.Count;
    }

    public async Task<IReadOnlyList<AgentRPA.Application.Scheduling.ExecutionNodeSnapshot>> GetOnlineNodesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var timeout = now.AddSeconds(-45);
        var onlineNodes = await db.ExecutionNodes.AsNoTracking().Where(x => x.Status == NodeStatus.Online).ToListAsync(cancellationToken);
        var nodes = onlineNodes.Where(x => x.LastHeartbeatAt >= timeout).ToList();
        var ids = nodes.Select(x => x.Id).ToArray();
        var capabilities = await db.NodeCapabilities.AsNoTracking().Where(x => ids.Contains(x.NodeId) && x.Enabled).ToListAsync(cancellationToken);
        var slots = await db.WorkerSlots.AsNoTracking().Where(x => ids.Contains(x.NodeId) && x.Enabled).ToListAsync(cancellationToken);
        return nodes.Select(node => new AgentRPA.Application.Scheduling.ExecutionNodeSnapshot(
            node.Id, node.Name, node.OsPlatform.ToString(), node.NodeKind.ToString(), node.Architecture, node.Status.ToString(), node.NodePoolId,
            node.NetworkZone, capabilities.Where(x => x.NodeId == node.Id).Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase),
            capabilities.Where(x => x.NodeId == node.Id && x.Code.StartsWith("UKey:", StringComparison.OrdinalIgnoreCase)).Select(x => x.Code[5..]).ToHashSet(StringComparer.OrdinalIgnoreCase),
            slots.Count(x => x.NodeId == node.Id && (x.ExecutionId is null || x.LeaseExpiresAt <= now)), 0d)).ToList();
    }

    private async Task ReplaceCapabilitiesInternalAsync(Guid nodeId, IEnumerable<NodeCapabilityInput> inputs, CancellationToken cancellationToken)
    {
        var old = await db.NodeCapabilities.Where(x => x.NodeId == nodeId).ToListAsync(cancellationToken);
        var requested = inputs.Where(x => !string.IsNullOrWhiteSpace(x.Code))
            .DistinctBy(x => x.Code.Trim(), StringComparer.OrdinalIgnoreCase).ToArray();
        db.NodeCapabilities.RemoveRange(old.Where(x => requested.All(input => !string.Equals(input.Code.Trim(), x.Code, StringComparison.OrdinalIgnoreCase))));
        foreach (var input in requested)
        {
            var existing = old.FirstOrDefault(x => string.Equals(x.Code, input.Code.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existing is null) db.NodeCapabilities.Add(new NodeCapability(nodeId, input.Code.Trim(), input.Version, input.MetadataJson));
            else existing.Refresh(input.Version, input.MetadataJson);
        }
    }

    private async Task ReplaceSlotsInternalAsync(Guid nodeId, IEnumerable<string> slotNames, CancellationToken cancellationToken)
    {
        var existing = await db.WorkerSlots.Where(x => x.NodeId == nodeId).ToListAsync(cancellationToken);
        var requested = slotNames.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var slot in existing.Where(x => !requested.Contains(x.SlotName)))
            if (slot.ExecutionId is null) db.WorkerSlots.Remove(slot);
        foreach (var name in requested.Where(name => existing.All(x => !string.Equals(x.SlotName, name, StringComparison.OrdinalIgnoreCase))))
            db.WorkerSlots.Add(new WorkerSlot(nodeId, name));
    }
}
