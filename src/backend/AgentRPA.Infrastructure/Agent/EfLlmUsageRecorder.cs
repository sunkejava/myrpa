using AgentRPA.Application.Agent;
using AgentRPA.Domain.Audit;
using AgentRPA.Infrastructure.Persistence;

namespace AgentRPA.Infrastructure.Agent;

/// <summary>EF Core LLM 用量记录器；不保存 Prompt/Response。</summary>
public sealed class EfLlmUsageRecorder(AgentRpaDbContext db) : ILlmUsageRecorder
{
    public async Task RecordAsync(
        Guid subjectId,
        string providerId,
        string model,
        LlmResponse response,
        Guid? taskId = null,
        Guid? taskItemId = null,
        CancellationToken cancellationToken = default)
    {
        if (subjectId == Guid.Empty || !response.Success) return;
        if (response.InputTokens <= 0 && response.OutputTokens <= 0) return;

        db.LlmUsageRecords.Add(new LlmUsageRecord(
            subjectId,
            providerId,
            model,
            response.InputTokens,
            response.OutputTokens,
            taskId,
            taskItemId));
        await db.SaveChangesAsync(cancellationToken);
    }
}
