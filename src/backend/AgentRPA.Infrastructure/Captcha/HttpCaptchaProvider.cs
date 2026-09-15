using System.Net.Http.Json;
using AgentRPA.Application.Abstractions;

namespace AgentRPA.Infrastructure.Captcha;

/// <summary>第三方验证码 HTTP Adapter。通过 ProviderId + endpoint 支持多供应商路由与故障切换。</summary>
public sealed class HttpCaptchaProvider(HttpClient client, string providerId, Uri endpoint) : ICaptchaProvider
{
    public string ProviderId => providerId;

    public async Task<CaptchaResult> RecognizeAsync(CaptchaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(endpoint, new
            {
                taskId = request.TaskId,
                type = request.Type,
                imageBase64 = Convert.ToBase64String(request.Image.ToArray())
            }, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, ErrorCode: $"HTTP_{(int)response.StatusCode}");
            var result = await response.Content.ReadFromJsonAsync<CaptchaResponse>(cancellationToken: cancellationToken);
            return result is null ? new(false, ErrorCode: "EMPTY_RESPONSE") : new(result.Success, result.Value, result.ErrorCode);
        }
        catch (Exception ex)
        {
            return new(false, ErrorCode: "CAPTCHA_PROVIDER_ERROR", Value: null);
        }
    }

    private sealed record CaptchaResponse(bool Success, string? Value, string? ErrorCode);
}
