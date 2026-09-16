using AgentRPA.Domain.Tasks;

namespace AgentRPA.Tests;

public sealed class TaskModelsTests
{
    [Fact]
    public void Retry_failed_item_increments_round_and_returns_pending()
    {
        var task = new RpaTask(Guid.NewGuid(), 1, "批量社保任务", maxRetries: 3);
        var item = task.AddItem("{\"name\":\"张三\"}");

        item.Fail("执行失败");

        Assert.True(item.CanRetry(task.MaxRetries));
        item.Retry();

        Assert.Equal(1, item.RetryCount);
        Assert.Equal(TaskItemStatus.Pending, item.Status);
        Assert.Null(item.ResultJson);
    }

    [Fact]
    public void Retry_is_denied_after_max_retries()
    {
        var task = new RpaTask(Guid.NewGuid(), 1, "任务", maxRetries: 1);
        var item = task.AddItem("{}");

        item.Fail("第一次失败");
        Assert.True(item.CanRetry(task.MaxRetries));
        item.Retry();
        item.Fail("第二次失败");

        Assert.False(item.CanRetry(task.MaxRetries));
        Assert.Equal(1, item.RetryCount);
    }

    [Fact]
    public void Execution_dispatch_sets_node_and_worker_and_status()
    {
        var execution = new Execution(Guid.NewGuid(), Guid.NewGuid(), "item:0");
        var nodeId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        execution.Dispatch(nodeId, slotId);

        Assert.Equal(ExecutionStatus.Dispatched, execution.Status);
        Assert.Equal(nodeId, execution.NodeId);
        Assert.Equal(slotId, execution.WorkerSlotId);
    }
}
