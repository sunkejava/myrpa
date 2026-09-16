using AgentRPA.Domain.Common;

namespace AgentRPA.Tests;

public sealed class SensitiveTextSanitizerTests
{
    [Theory]
    [InlineData("password=abc123", "password=[REDACTED]")]
    [InlineData("token: secret-value", "token: [REDACTED]")]
    [InlineData("Authorization: Bearer abc.def", "Authorization: [REDACTED]")]
    [InlineData("Cookie: sid=abc; auth=def", "Cookie: [REDACTED]")]
    public void Sensitive_values_are_redacted(string input, string expected) => Assert.Equal(expected, SensitiveTextSanitizer.Sanitize(input));

    [Fact]
    public void Diagnostic_text_is_truncated()
    {
        var result = SensitiveTextSanitizer.Sanitize(new string('a', 2100));
        Assert.NotNull(result);
        Assert.Equal(2001, result!.Length);
        Assert.Equal('…', result[^1]);
    }
}
