namespace AgentRPA.Domain.Mock;

/// <summary>开发环境社保站点的参保记录。</summary>
public sealed class MockSocialEmployee
{
    public string IdNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>开发环境社保站点已成功受理的业务请求。</summary>
public sealed class MockSocialReceipt
{
    public string SubmissionId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
