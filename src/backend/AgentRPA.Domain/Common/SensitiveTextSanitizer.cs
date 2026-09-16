using System.Text.RegularExpressions;

namespace AgentRPA.Domain.Common;

/// <summary>执行结果/错误摘要脱敏器。默认只保留可诊断信息，避免密码、Token、Cookie 等秘密进入数据库。</summary>
public static partial class SensitiveTextSanitizer
{
    private const string Replacement = "[REDACTED]";

    public static string? Sanitize(string? value, int maxLength = 2000)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var text = value.Trim();
        text = AuthorizationHeader().Replace(text, $"$1{Replacement}");
        text = KeyValueSecret().Replace(text, $"$1{Replacement}");
        text = CookieHeader().Replace(text, $"$1{Replacement}");
        text = BearerToken().Replace(text, $"$1{Replacement}");
        text = text.Length > maxLength ? text[..maxLength] + "…" : text;
        return text;
    }

    [GeneratedRegex(@"(?i)(authorization\s*:\s*)([^\s,;]+)", RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeader();

    [GeneratedRegex(@"(?i)(\b(?:password|passwd|pwd|token|access_token|refresh_token|secret|client_secret|api[_-]?key|pin)\s*[=:]\s*)([^\s,;&]+)", RegexOptions.CultureInvariant)]
    private static partial Regex KeyValueSecret();

    [GeneratedRegex(@"(?i)(cookie\s*:\s*)([^\r\n]+)", RegexOptions.CultureInvariant)]
    private static partial Regex CookieHeader();

    [GeneratedRegex(@"(?i)(bearer\s+)[A-Za-z0-9._~+/=-]+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerToken();
}
