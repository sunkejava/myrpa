using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.Audit;

/// <summary>记录 LLM 调用的 token 用量；默认不保存 Prompt/Response，避免把业务敏感内容写入统计库。</summary>
public sealed class LlmUsageRecord : Entity
{
    private LlmUsageRecord() { }

    public LlmUsageRecord(
        Guid subjectId,
        string providerId,
        string model,
        int inputTokens,
        int outputTokens,
        Guid? taskId = null,
        Guid? taskItemId = null)
    {
        SubjectId = subjectId;
        ProviderId = providerId.Trim();
        Model = model.Trim();
        InputTokens = Math.Max(0, inputTokens);
        OutputTokens = Math.Max(0, outputTokens);
        TaskId = taskId;
        TaskItemId = taskItemId;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public Guid SubjectId { get; private set; }
    public Guid? TaskId { get; private set; }
    public Guid? TaskItemId { get; private set; }
    public string ProviderId { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int TotalTokens => InputTokens + OutputTokens;
    public DateTimeOffset OccurredAt { get; private set; }
}
