namespace AgentRPA.NodeAgent.Execution;

/// <summary>站点适配层：将 Workflow 的语义定位符映射为具体浏览器地址和 CSS 选择器。</summary>
public interface IWorkflowSiteAdapter
{
    string Code { get; }
    string ResolveUrl(string url, IReadOnlyDictionary<string, string?> parameters);
    string ResolveSelector(string selector);
}

/// <summary>默认保持原始 URL/选择器，兼容已有 Workflow。</summary>
public sealed class DirectWorkflowSiteAdapter : IWorkflowSiteAdapter
{
    public string Code => "direct";
    public string ResolveUrl(string url, IReadOnlyDictionary<string, string?> parameters) => url;
    public string ResolveSelector(string selector) => selector;
}

/// <summary>开发环境青岛社保模拟站点的浏览器定位符。</summary>
public sealed class QingdaoSocialSecuritySiteAdapter : IWorkflowSiteAdapter
{
    private static readonly IReadOnlyDictionary<string, string> Selectors = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["@login.account"] = "[name=username]",
        ["@login.password"] = "[name=password]",
        ["@login.submit"] = "form[action$='/login'] button[type=submit]",
        ["@employee.operation"] = "[name=operation]",
        ["@employee.name"] = "[name=employeeName]",
        ["@employee.id"] = "[name=idNumber]",
        ["@employee.submit"] = "form[action$='/employees'] button[type=submit]",
        ["@employee.success"] = "[data-result=success]"
    };

    public string Code => "qd-social-security";

    public string ResolveUrl(string url, IReadOnlyDictionary<string, string?> parameters)
    {
        if (!url.StartsWith("mock://", StringComparison.Ordinal)) return url;
        if (!parameters.TryGetValue("mockBaseUrl", out var baseUrl) ||
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("青岛社保 Adapter 需要有效的 mockBaseUrl。");
        return url switch
        {
            "mock://login" => baseUrl!.TrimEnd('/') + "/mock/qd-social-security",
            _ => throw new InvalidOperationException($"青岛社保 Adapter 不支持地址：{url}")
        };
    }

    public string ResolveSelector(string selector) => Selectors.TryGetValue(selector, out var mapped) ? mapped
        : selector.StartsWith('@') ? throw new InvalidOperationException($"青岛社保 Adapter 不支持选择器：{selector}") : selector;
}
