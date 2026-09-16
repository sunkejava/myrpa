using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.Execution;

/// <summary>执行日志级别。</summary>
public enum ExecutionLogLevel
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3
}

/// <summary>执行日志事件类型，用于前端时间线和后续统计。</summary>
public enum ExecutionLogEventType
{
    Execution = 0,
    StepStarted = 1,
    StepCompleted = 2,
    StepFailed = 3,
    Browser = 4,
    HumanIntervention = 5,
    Artifact = 6,
    System = 7
}

/// <summary>执行过程日志。Sequence 是 SQLite 下稳定排序的业务序号，避免按 DateTimeOffset 排序。</summary>
public sealed class ExecutionLog : Entity
{
    private ExecutionLog() { }

    public ExecutionLog(
        Guid executionId,
        long sequence,
        ExecutionLogLevel level,
        ExecutionLogEventType eventType,
        string message,
        string? stepId = null,
        string? metadataJson = null,
        bool sensitive = false)
    {
        if (executionId == Guid.Empty) throw new ArgumentException("ExecutionId 不能为空。", nameof(executionId));
        if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("日志消息不能为空。", nameof(message));
        if (message.Length > 4000) throw new ArgumentException("日志消息不能超过 4000 个字符。", nameof(message));

        ExecutionId = executionId;
        Sequence = sequence;
        Level = level;
        EventType = eventType;
        StepId = stepId;
        Message = message.Trim();
        MetadataJson = metadataJson;
        Sensitive = sensitive;
    }

    public Guid ExecutionId { get; private set; }
    public long Sequence { get; private set; }
    public ExecutionLogLevel Level { get; private set; }
    public ExecutionLogEventType EventType { get; private set; }
    public string? StepId { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }
    public bool Sensitive { get; private set; }
}

/// <summary>执行过程中产生的文件、截图、下载结果等产物元数据。</summary>
public sealed class ExecutionArtifact : Entity
{
    private ExecutionArtifact() { }

    public ExecutionArtifact(
        Guid executionId,
        Guid taskItemId,
        string artifactType,
        string fileName,
        string storageKey,
        string? contentType,
        long size,
        string? sha256,
        DateTimeOffset? expiresAt = null)
    {
        if (executionId == Guid.Empty) throw new ArgumentException("ExecutionId 不能为空。", nameof(executionId));
        if (taskItemId == Guid.Empty) throw new ArgumentException("TaskItemId 不能为空。", nameof(taskItemId));
        if (string.IsNullOrWhiteSpace(artifactType)) throw new ArgumentException("产物类型不能为空。", nameof(artifactType));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("文件名不能为空。", nameof(fileName));
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("StorageKey 不能为空。", nameof(storageKey));
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));

        ExecutionId = executionId;
        TaskItemId = taskItemId;
        ArtifactType = artifactType.Trim();
        FileName = fileName.Trim();
        StorageKey = storageKey.Trim();
        ContentType = contentType?.Trim();
        Size = size;
        Sha256 = sha256?.Trim();
        ExpiresAt = expiresAt;
    }

    public Guid ExecutionId { get; private set; }
    public Guid TaskItemId { get; private set; }
    public string ArtifactType { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string? ContentType { get; private set; }
    public long Size { get; private set; }
    public string? Sha256 { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
}
