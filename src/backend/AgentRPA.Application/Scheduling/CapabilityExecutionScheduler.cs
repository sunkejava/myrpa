namespace AgentRPA.Application.Scheduling;

/// <summary>默认调度实现：先硬过滤，再软评分，并通过 Lease 原子占用 WorkerSlot。</summary>
public sealed class CapabilityExecutionScheduler(
    IExecutionNodeRegistry nodeRegistry,
    IExecutionLeaseService leaseService) : IExecutionScheduler
{
    public async Task<ExecutionAssignment?> ScheduleAsync(
        ExecutionRequirement requirement,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        var nodes = await nodeRegistry.GetOnlineNodesAsync(cancellationToken);

        var candidates = nodes
            .Where(node => IsEligible(node, requirement))
            .Select(node => (Node: node, Score: Score(node, requirement)))
            .OrderByDescending(x => x.Score)
            .ToList();

        foreach (var candidate in candidates)
        {
            var assignment = await leaseService.TryAcquireAsync(
                candidate.Node,
                executionId,
                cancellationToken);

            if (assignment is not null)
                return assignment with { Score = candidate.Score };
        }

        // 没有可用资源时返回 null，由上层将 Execution 保持在 WaitingForResource。
        return null;
    }

    private static bool IsEligible(ExecutionNodeSnapshot node, ExecutionRequirement requirement)
    {
        if (!string.Equals(node.Status, "Online", StringComparison.OrdinalIgnoreCase))
            return false;

        if (node.AvailableSlots <= 0)
            return false;

        if (requirement.OsPlatforms.Count > 0 && !requirement.OsPlatforms.Contains(node.OsPlatform, StringComparer.OrdinalIgnoreCase))
            return false;

        if (requirement.NodeKinds.Count > 0 && !requirement.NodeKinds.Contains(node.NodeKind, StringComparer.OrdinalIgnoreCase))
            return false;

        if (requirement.NetworkZone is not null && !string.Equals(requirement.NetworkZone, node.NetworkZone, StringComparison.OrdinalIgnoreCase))
            return false;

        if (requirement.NodePoolId.HasValue && requirement.NodePoolId != node.NodePoolId)
            return false;

        if (requirement.RequiredNodeIds is { Count: > 0 } && !requirement.RequiredNodeIds.Contains(node.NodeId))
            return false;

        if (requirement.ExcludedNodeIds?.Contains(node.NodeId) == true)
            return false;

        if (requirement.RequiresDesktopUi && !Has(node, "DesktopUI"))
            return false;

        if (requirement.RequiredCapabilities.Any(code => !Has(node, code)))
            return false;

        if (requirement.ForbiddenCapabilities.Any(Has))
            return false;

        if (requirement.RequiredHardwareIds is { Count: > 0 } && !requirement.RequiredHardwareIds.IsSubsetOf(node.HardwareIds))
            return false;

        return true;
    }

    private static int Score(ExecutionNodeSnapshot node, ExecutionRequirement requirement)
    {
        var score = 100;
        score += (int)((1d - Math.Clamp(node.LoadFactor, 0, 1)) * 50);

        if (requirement.RequiredNodeIds?.Contains(node.NodeId) == true)
            score += 1000;

        if (requirement.NodePoolId == node.NodePoolId)
            score += 20;

        if (requirement.RequiredHardwareIds is { Count: > 0 })
            score += 50;

        return score;
    }

    private static bool Has(ExecutionNodeSnapshot node, string capability)
        => node.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase);
}
