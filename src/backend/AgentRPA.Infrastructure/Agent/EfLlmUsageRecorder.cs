using AgentRPA.Application.Agent;
using AgentRPA.Domain.Audit;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace AgentRPA.Infrastructure.Agent;

/// <summary>EF Core LLM 用量记录器；失败调用也不记录 Prompt/Response 内容。</summary>
public sealed class EfLlmUsageRecorder(AgentRpaDbContext db, IOptions<LlmProviderOptions> options) : ILlmUsageRecorder
{
    public async Task RecordAsync(
        Guid subjectId,
        LlmResponse response,
        Guid? taskId = null,
        Guid? taskItemId = null,
        CancellationToken cancellationToken = default)
    {
        if (subjectId == Guid.Empty || !response.Success) return;
        if (response.InputTokens <= 0 && response.OutputTokens <= 0) return;

        var settings = options.Value;
        db.LlmUsageRecords.Add(new LlmUsageRecord(
            subjectId,
            "openai-compatible",
            string.IsNullOrWhiteSpace(settings.Model) ? "configured" : settings.Model,
            response.InputTokens,
            response.OutputTokens,
            taskId,
            taskItemId));
        await db.SaveChangesAsync(cancellationToken);
    }
}
