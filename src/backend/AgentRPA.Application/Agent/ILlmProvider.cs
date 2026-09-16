namespace AgentRPA.Application.Agent;

/// <summary>统一 LLM Provider 抽象，Agent 不绑定具体模型或厂商 SDK。</summary>
public interface ILlmProvider
{
    string ProviderId { get; }
    /// <summary>Provider 当前使用的模型标识；自定义 Provider 未提供时允许使用 configured。</summary>
    string Model => "configured";
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken);
}

public sealed record LlmRequest(string SystemPrompt, string UserPrompt, double Temperature = 0.1, int? MaxTokens = null);
public sealed record LlmResponse(bool Success, string Content, int InputTokens = 0, int OutputTokens = 0, string? Error = null);
