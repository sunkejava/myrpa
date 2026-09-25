using AgentRPA.Domain.Common;
namespace AgentRPA.Domain.Tasks;
public enum TaskStatus { Draft, WaitingForResource, Queued, Running, Paused, WaitingForHuman, Succeeded, Failed, Cancelled }
public enum TaskItemStatus { Pending, Running, Succeeded, Failed, Skipped }
public enum ExecutionStatus { Pending, Dispatched, Running, Paused, WaitingForHuman, Succeeded, Failed, Cancelled }
public sealed class RpaTask : Entity
{
    private readonly List<TaskItem> _items = [];
    private RpaTask() { }
    public RpaTask(Guid workflowId, int workflowVersion, string name, int maxRetries = 3, Guid? subjectId = null) { WorkflowId = workflowId; WorkflowVersion = workflowVersion; Name = name; MaxRetries = Math.Clamp(maxRetries, 0, 20); SubjectId = subjectId; }
    public Guid WorkflowId { get; private set; }
    public int WorkflowVersion { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int MaxRetries { get; private set; } = 3;
    public Guid? SubjectId { get; private set; }
    public TaskStatus Status { get; private set; } = TaskStatus.Draft;
    public IReadOnlyCollection<TaskItem> Items => _items;
    public TaskItem AddItem(string inputJson) { var item = new TaskItem(Id, _items.Count + 1, inputJson); _items.Add(item); return item; }
    public void Queue() => Status = TaskStatus.Queued;
    public void SetStatus(TaskStatus status) => Status = status;
}
public sealed class TaskItem : Entity
{
    private TaskItem() { }
    public TaskItem(Guid taskId, int sequence, string inputJson) { TaskId = taskId; Sequence = sequence; InputJson = inputJson; }
    public Guid TaskId { get; private set; }
    public int Sequence { get; private set; }
    public string InputJson { get; private set; } = "{}";
    public TaskItemStatus Status { get; private set; } = TaskItemStatus.Pending;
    public string? ResultJson { get; private set; }
    public int RetryCount { get; private set; }
    public void Start() => Status = TaskItemStatus.Running;
    public void Succeed(string? resultJson = null) { Status = TaskItemStatus.Succeeded; ResultJson = resultJson; }
    public void Fail(string? resultJson = null) { Status = TaskItemStatus.Failed; ResultJson = resultJson; }
    public void Skip() => Status = TaskItemStatus.Skipped;
    public bool CanRetry(int maxRetries) => Status == TaskItemStatus.Failed && RetryCount < maxRetries;
    public void Retry() { RetryCount++; Status = TaskItemStatus.Pending; ResultJson = null; }
}
public sealed class Execution : Entity
{
    private Execution() { }
    public Execution(Guid taskItemId, Guid workflowVersionId, string dispatchKey) { TaskItemId = taskItemId; WorkflowVersionId = workflowVersionId; DispatchKey = dispatchKey; }
    public Guid TaskItemId { get; private set; }
    public Guid WorkflowVersionId { get; private set; }
    /// <summary>同一 TaskItem 同一重试轮次只允许产生一个 Execution。</summary>
    public string DispatchKey { get; private set; } = string.Empty;
    public Guid? NodeId { get; private set; }
    public Guid? WorkerSlotId { get; private set; }
    public ExecutionStatus Status { get; private set; } = ExecutionStatus.Pending;
    public string? Error { get; private set; }
    public void Dispatch(Guid nodeId, Guid workerSlotId) { NodeId = nodeId; WorkerSlotId = workerSlotId; Status = ExecutionStatus.Dispatched; }
    public void Start() => Status = ExecutionStatus.Running;
    public void SetStatus(ExecutionStatus status, string? error = null) { Status = status; Error = error; }
}
