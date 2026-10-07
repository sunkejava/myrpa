using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace AgentRPA.NodeAgent.Execution;

/// <summary>从 Bing 结果进入 ModelScope，操作站内搜索，准确选择仓库和 GGUF 文件。</summary>
public sealed class ModelDownloadStep(ModelFileDownloader downloader, ModelDownloadOptions options)
{
    public async Task<ModelDownloadReceipt> ExecuteAsync(IPage page, JsonElement config, IReadOnlyDictionary<string, string?> parameters,
        Func<string, Task> progress, CancellationToken ct)
    {
        string Required(string key)
        {
            var value = config.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
            foreach (var item in parameters) value = value?.Replace("{{" + item.Key + "}}", item.Value ?? "", StringComparison.Ordinal);
            return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException("ModelDownload 缺少 " + key) : value;
        }
        var query = Required("modelQuery");
        var repository = Required("repository");
        var filename = Required("fileName");
        var revision = Required("revision");
        if (!int.TryParse(Required("downloadTimeoutSeconds"), out var timeout)) throw new InvalidOperationException("下载超时必须是秒数。");
        ModelFileDownloader.ValidateIdentity(repository, revision, filename);
        var catalog = new Uri(options.CatalogBaseUrl.TrimEnd('/') + "/");
        await progress("在 Bing 搜索结果中定位 ModelScope 网站。");
        bool IsCatalog(Uri uri) => uri.Port == catalog.Port && uri.Scheme == catalog.Scheme &&
            (uri.Host == catalog.Host || catalog.Host == "modelscope.cn" && uri.Host == "www.modelscope.cn");
        var results = page.Locator("#b_results .b_algo h2 a").Filter(new LocatorFilterOptions { HasTextRegex = new Regex("ModelScope|魔搭|modelspace", RegexOptions.IgnoreCase) });
        await results.First.WaitForAsync();
        string? href = null;
        foreach (var candidate in (await results.AllAsync()).Take(20))
        {
            var link = await candidate.GetAttributeAsync("href");
            if (link is null) continue;
            var destination = new Uri(new Uri(page.Url), link);
            // Bing ck/a 链接在 u 参数中公开编码实际目标；只接受配置站点，避免误入同名结果。
            if (destination.Host.EndsWith(".bing.com", StringComparison.Ordinal) && destination.AbsolutePath == "/ck/a")
            {
                var encoded = destination.Query.TrimStart('?').Split('&').FirstOrDefault(x => x.StartsWith("u=", StringComparison.Ordinal));
                if (encoded is not null)
                {
                    var value = Uri.UnescapeDataString(encoded[2..]);
                    if (value.StartsWith("a1", StringComparison.Ordinal))
                        try
                        {
                            var base64 = value[2..].Replace('-', '+').Replace('_', '/');
                            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
                            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                            if (Uri.TryCreate(decoded, UriKind.Absolute, out var target)) destination = target;
                        }
                        catch (FormatException) { }
                }
            }
            if (IsCatalog(destination)) { href = link; break; }
        }
        if (href is null) throw new InvalidOperationException("Bing 搜索结果未包含已配置的 ModelScope 官方站点。");
        // 使用搜索结果的真实链接；同页进入，避免弹出页面使后续步骤操作旧标签。
        await page.GotoAsync(new Uri(new Uri(page.Url), href).AbsoluteUri);
        await page.WaitForURLAsync(url => Uri.TryCreate(url, UriKind.Absolute, out var destination) && IsCatalog(destination));
        if (!IsCatalog(new Uri(page.Url)))
            throw new InvalidOperationException("Bing 搜索结果未进入已配置的 ModelScope 域名，停止下载。");
        ct.ThrowIfCancellationRequested();
        await progress("已进入 ModelScope，开始站内搜索：" + query);
        await page.GotoAsync(new Uri(catalog, "models").AbsoluteUri);
        var search = page.Locator("input[placeholder*='搜索您想要的模型']").First;
        await search.FillAsync(query);
        await search.PressAsync("Enter");
        var repositoryLink = page.Locator("a[href='/models/" + repository + "']").First;
        // 数据卡片中的 license 标签有独立点击行为，读取已命中仓库链接后再导航。
        await repositoryLink.WaitForAsync();
        var repositoryHref = await repositoryLink.GetAttributeAsync("href");
        var expectedPath = "/models/" + repository;
        if (repositoryHref is null || new Uri(catalog, repositoryHref).AbsolutePath.TrimEnd('/') != expectedPath)
            throw new InvalidOperationException("站内搜索未命中指定仓库，请检查 modelQuery 与 repository。");
        await page.GotoAsync(new Uri(catalog, repositoryHref).AbsoluteUri);
        await progress("命中模型仓库：" + repository);
        await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "模型文件", Exact = true }).ClickAsync();
        // 只匹配目标权重，不把 mmproj、imatrix 或另一量化文件当作模型。
        foreach (var directory in filename.Split('/').SkipLast(1))
            await page.GetByText(directory, new PageGetByTextOptions { Exact = true }).First.ClickAsync();
        var fileLink = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { NameRegex = new Regex("^" + Regex.Escape(Path.GetFileName(filename)) + @"(?:\s+GGUF)?$", RegexOptions.IgnoreCase) });
        await fileLink.WaitForAsync();
        await progress("模型文件页面已找到：" + filename + "；下载版本：" + revision);
        return await downloader.DownloadAsync(repository, revision, filename, timeout, progress, ct);
    }
}
