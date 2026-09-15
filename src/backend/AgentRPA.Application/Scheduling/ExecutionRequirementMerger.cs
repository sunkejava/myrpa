namespace AgentRPA.Application.Scheduling;

/// <summary>将 BusinessSystem 默认要求与 WorkflowVersion 要求合并为最终执行要求。</summary>
public static class ExecutionRequirementMerger
{
    /// <summary>
    /// WorkflowVersion 只能收紧 BusinessSystem 的要求，不能放宽系统安全边界。
    /// 集合字段采用交集语义；未设置的 Workflow 字段继承系统默认值。
    /// </summary>
    public static ExecutionRequirement Merge(
        ExecutionRequirement systemRequirement,
        ExecutionRequirement? workflowRequirement)
    {
        if (workflowRequirement is null)
            return systemRequirement;

        var os = IntersectOrInherit(systemRequirement.OsPlatforms, workflowRequirement.OsPlatforms);
        var kinds = IntersectOrInherit(systemRequirement.NodeKinds, workflowRequirement.NodeKinds);
        var browsers = IntersectOrInherit(systemRequirement.Browsers, workflowRequirement.Browsers);
        var required = Union(systemRequirement.RequiredCapabilities, workflowRequirement.RequiredCapabilities);
        var forbidden = Union(systemRequirement.ForbiddenCapabilities, workflowRequirement.ForbiddenCapabilities);

        var networkZone = workflowRequirement.NetworkZone ?? systemRequirement.NetworkZone;
        var pool = workflowRequirement.NodePoolId ?? systemRequirement.NodePoolId;
        var requiredNodes = IntersectOrInherit(systemRequirement.RequiredNodeIds, workflowRequirement.RequiredNodeIds);
        var excludedNodes = Union(systemRequirement.ExcludedNodeIds, workflowRequirement.ExcludedNodeIds);
        var hardware = Union(systemRequirement.RequiredHardwareIds, workflowRequirement.RequiredHardwareIds);

        return workflowRequirement with
        {
            OsPlatforms = os,
            NodeKinds = kinds,
            Browsers = browsers,
            RequiredCapabilities = required,
            ForbiddenCapabilities = forbidden,
            NetworkZone = networkZone,
            NodePoolId = pool,
            RequiredNodeIds = requiredNodes,
            ExcludedNodeIds = excludedNodes,
            RequiredHardwareIds = hardware,
            RequiresDesktopUi = systemRequirement.RequiresDesktopUi || workflowRequirement.RequiresDesktopUi
        };
    }

    private static IReadOnlySet<string> IntersectOrInherit(
        IReadOnlySet<string> parent,
        IReadOnlySet<string> child)
        => child.Count == 0
            ? parent
            : parent.Count == 0
                ? child
                : parent.Intersect(child, StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<T> IntersectOrInherit<T>(
        IReadOnlySet<T>? parent,
        IReadOnlySet<T>? child)
    {
        if (child is null || child.Count == 0)
            return parent ?? new HashSet<T>();

        if (parent is null || parent.Count == 0)
            return child;

        return parent.Intersect(child).ToHashSet();
    }

    private static IReadOnlySet<T> Union<T>(
        IReadOnlySet<T>? first,
        IReadOnlySet<T>? second)
        => (first ?? new HashSet<T>())
            .Concat(second ?? new HashSet<T>())
            .ToHashSet();
}
