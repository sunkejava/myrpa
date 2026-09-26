using System.Text.RegularExpressions;

namespace AgentRPA.NodeAgent.Execution;

/// <summary>节点本地站点配置；页面定位映射由运维更新，无需修改执行引擎。</summary>
public sealed class ConfiguredSiteAdapterOptions
{
    public string Code { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Paths { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Selectors { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ConfiguredWorkflowSiteAdapter : IWorkflowSiteAdapter
{
    private static readonly Regex CodePattern = new("^[a-z][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private readonly Uri baseUri;
    private readonly IReadOnlyDictionary<string, string> paths;
    private readonly IReadOnlyDictionary<string, string> selectors;

    public ConfiguredWorkflowSiteAdapter(ConfiguredSiteAdapterOptions options)
    {
        if (!CodePattern.IsMatch(options.Code) || options.Code is "direct" or "qd-social-security")
            throw new InvalidOperationException("站点 Adapter 编码无效或与内置 Adapter 冲突。");
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException($"站点 Adapter {options.Code} 的 BaseUrl 必须是无凭据的 HTTP/HTTPS 地址。");
        Code = options.Code;
        baseUri = uri;
        paths = new Dictionary<string, string>(options.Paths ?? [], StringComparer.Ordinal);
        selectors = new Dictionary<string, string>(options.Selectors ?? [], StringComparer.Ordinal);
        if (paths.Concat(selectors).Any(x => !x.Key.StartsWith('@') || x.Key.Length is < 2 or > 128 ||
            string.IsNullOrWhiteSpace(x.Value) || x.Value.Length > 2048))
            throw new InvalidOperationException($"站点 Adapter {Code} 的语义定位符配置无效。");
        foreach (var path in paths.Values) _ = ResolveSiteUrl(path);
    }

    public string Code { get; }

    public string ResolveUrl(string url, IReadOnlyDictionary<string, string?> parameters)
    {
        if (url.StartsWith('@'))
        {
            if (!paths.TryGetValue(url, out var path)) throw new InvalidOperationException($"站点 {Code} 未配置页面：{url}");
            url = path;
        }
        return ResolveSiteUrl(url).AbsoluteUri;
    }

    public string ResolveSelector(string selector) => selector.StartsWith('@')
        ? selectors.TryGetValue(selector, out var mapped) ? mapped : throw new InvalidOperationException($"站点 {Code} 未配置选择器：{selector}")
        : selector;

    private Uri ResolveSiteUrl(string path)
    {
        if (path.StartsWith("//", StringComparison.Ordinal) || !Uri.TryCreate(baseUri, path, out var target) ||
            target.Scheme != baseUri.Scheme || target.Host != baseUri.Host || target.Port != baseUri.Port ||
            !string.IsNullOrEmpty(target.UserInfo) || target.AbsolutePath.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidOperationException($"站点 {Code} 禁止访问配置站点之外的地址。");
        return target;
    }
}
