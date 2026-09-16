namespace AgentRPA.Application.Agent;

/// <summary>LLM 用量记录器。业务层只提交统计信息，不保存 Prompt/Response。</summary>
public interface ILlmUsageRecorder
{
    Task RecordAsync(
        Guid subjectId,
        string providerId,
        string model,
        LlmResponse response,
        Guid? taskId = null,
        Guid? taskItemId = null,
        CancellationToken cancellationToken = default);
}
