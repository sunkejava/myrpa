using AgentRPA.Application.Scheduling;

namespace AgentRPA.Tests;

public sealed class CapabilityExecutionSchedulerTests
{
    [Fact]
    public async Task Scheduler_filters_offline_nodes_and_missing_capabilities()
    {
        var validNode = Node("Windows", "Physical", "Online", ["DesktopUI", "UKey"], ["UKey-001"], slots: 1);
        var registry = new FakeNodeRegistry(
            Node("Windows", "Physical", "Offline", ["DesktopUI", "UKey"], ["UKey-001"], slots: 1),
            Node("Windows", "Physical", "Online", ["DesktopUI"], [], slots: 1),
            validNode);
        var lease = new FakeLeaseService(validNode);
        var scheduler = new CapabilityExecutionScheduler(registry, lease);

        var result = await scheduler.ScheduleAsync(
            new ExecutionRequirement(
                new HashSet<string>(["Windows"]),
                new HashSet<string>(["Physical"]),
                new HashSet<string>(["Chrome"]),
                new HashSet<string>(["UKey"]),
                new HashSet<string>(),
                RequiredHardwareIds: new HashSet<string>(["UKey-001"]),
                RequiresDesktopUi: true),
            Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(validNode.NodeId, result.NodeId);
        Assert.Equal(new[] { "UKey-001" }, lease.LastRequiredHardwareIds);
    }

    [Fact]
    public async Task Scheduler_respects_excluded_and_required_node_constraints()
    {
        var first = Node("Windows", "Physical", "Online", ["Browser.Chrome"], [], slots: 1);
        var second = Node("Windows", "Physical", "Online", ["Browser.Chrome"], [], slots: 1);
        var registry = new FakeNodeRegistry(first, second);
        var lease = new FakeLeaseService(second);
        var scheduler = new CapabilityExecutionScheduler(registry, lease);

        var result = await scheduler.ScheduleAsync(
            new ExecutionRequirement(
                new HashSet<string>(),
                new HashSet<string>(),
                new HashSet<string>(),
                new HashSet<string>(["Browser.Chrome"]),
                new HashSet<string>(),
                RequiredNodeIds: new HashSet<Guid>([second.NodeId]),
                ExcludedNodeIds: new HashSet<Guid>([first.NodeId])),
            Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(second.NodeId, result.NodeId);
    }

    private static ExecutionNodeSnapshot Node(
        string os,
        string kind,
        string status,
        string[] capabilities,
        string[] hardwareIds,
        int slots,
        double load = 0.1)
        => new(Guid.NewGuid(), "node", os, kind, "x64", status, null, null,
            new HashSet<string>(capabilities), new HashSet<string>(hardwareIds), slots, load);

    private sealed class FakeNodeRegistry(params ExecutionNodeSnapshot[] nodes) : IExecutionNodeRegistry
    {
        public Task<IReadOnlyList<ExecutionNodeSnapshot>> GetOnlineNodesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ExecutionNodeSnapshot>>(nodes);
    }

    private sealed class FakeLeaseService(ExecutionNodeSnapshot expectedNode) : IExecutionLeaseService
    {
        public IReadOnlyList<string>? LastRequiredHardwareIds { get; private set; }

        public Task<ExecutionAssignment?> TryAcquireAsync(ExecutionNodeSnapshot node, Guid executionId, IReadOnlySet<string>? requiredHardwareIds, CancellationToken cancellationToken)
        {
            if (node.NodeId != expectedNode.NodeId) return Task.FromResult<ExecutionAssignment?>(null);
            LastRequiredHardwareIds = requiredHardwareIds?.ToArray();
            return Task.FromResult<ExecutionAssignment?>(new(node.NodeId, Guid.NewGuid(), Guid.NewGuid(), 100));
        }

        public Task<bool> RenewAsync(Guid leaseId, Guid executionId, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task ReleaseAsync(Guid leaseId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
