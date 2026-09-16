using System.Net.Http.Json;
using System.Text.Json;
using AgentRPA.Application.Agent;
using Microsoft.Extensions.Options;

namespace AgentRPA.Infrastructure.Agent;

public sealed class LlmProviderOptions
{
    public string Endpoint { get; set; } = "http://127.0.0.1:8888/v1/chat/completions";
    public string Model { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>OpenAI-compatible Provider，兼容 llama.cpp server 等本地推理服务。</summary>
public sealed class OpenAiCompatibleLlmProvider(IHttpClientFactory clients, IOptions<LlmProviderOptions> options) : ILlmProvider
{
    public string ProviderId => "openai-compatible";
    public string Model => options.Value.Model;

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Endpoint)) return new(false, string.Empty, Error: "LLM Endpoint 未配置。");
        if (string.IsNullOrWhiteSpace(settings.Model)) return new(false, string.Empty, Error: "LLM Model 未配置。");
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
        requestMessage.Headers.Accept.ParseAdd("application/json");
        if (!string.IsNullOrWhiteSpace(settings.ApiKey)) requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ApiKey);
        requestMessage.Content = JsonContent.Create(new { model = settings.Model, temperature = request.Temperature, max_tokens = request.MaxTokens, messages = new[] { new { role = "system", content = request.SystemPrompt }, new { role = "user", content = request.UserPrompt } } });
        try
        {
            var client = clients.CreateClient("llm");
            using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, string.Empty, Error: $"LLM HTTP {(int)response.StatusCode}: {body[..Math.Min(body.Length, 500)]}");
            using var document = JsonDocument.Parse(body);
            var content = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            var usage = document.RootElement.TryGetProperty("usage", out var u) ? u : default;
            var input = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
            var output = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0;
            return new(true, content, input, output);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(false, string.Empty, Error: "LLM 请求超时。"); }
        catch (HttpRequestException ex) { return new(false, string.Empty, Error: $"LLM 连接失败：{ex.Message}"); }
        catch (JsonException ex) { return new(false, string.Empty, Error: $"LLM 返回 JSON 无法解析：{ex.Message}"); }
    }
}
