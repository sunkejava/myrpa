using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Playwright;

namespace AgentRPA.NodeAgent.Execution;

public interface IWorkflowRuntime
{
    Task ExecuteAsync(string definitionJson, IReadOnlyDictionary<string, string?> parameters, Func<WorkflowRuntimeEvent, Task> report, CancellationToken cancellationToken);
}

/// <summary>Workflow Runtime 事件；Artifact 仅携带受控本地产物的元数据，不携带文件内容。</summary>
public sealed record WorkflowRuntimeEvent(
    string Status,
    string? StepId,
    int ProgressPercent,
    string? Message,
    WorkflowRuntimeArtifact? Artifact = null);

public sealed record WorkflowRuntimeArtifact(
    string ArtifactType,
    string FileName,
    string StorageKey,
    string? ContentType,
    long Size,
    string? Hash);

/// <summary>基于 Playwright 的确定性浏览器 Workflow Runtime。</summary>
public sealed class PlaywrightWorkflowRuntime : IWorkflowRuntime
{
    public async Task ExecuteAsync(string definitionJson, IReadOnlyDictionary<string, string?> parameters, Func<WorkflowRuntimeEvent, Task> report, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson);
        var root = document.RootElement;
        var steps = GetSteps(root);
        if (steps.Length == 0) { await report(new("Succeeded", null, 100, "Workflow 没有可执行步骤。")); return; }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        try
        {
            await ExecuteStepsAsync(page, steps, parameters, report, cancellationToken, 0);
            await report(new("Succeeded", null, 100, "Workflow 执行完成。"));
        }
        finally { await browser.CloseAsync(); }
    }

    private static async Task ExecuteStepsAsync(IPage page, IReadOnlyList<JsonElement> steps, IReadOnlyDictionary<string, string?> parameters, Func<WorkflowRuntimeEvent, Task> report, CancellationToken cancellationToken, int depth)
    {
        if (depth > 8) throw new InvalidOperationException("Workflow 嵌套深度超过安全限制。");
        for (var index = 0; index < steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = steps[index];
            var type = GetString(step, "type") ?? "Wait";
            var id = GetString(step, "id") ?? $"step-{index + 1}";
            var config = step.TryGetProperty("config", out var cfg) ? cfg : default;
            var timeoutMs = Math.Clamp(GetInt(step, "timeoutMs") ?? 30_000, 100, 120_000);
            var retries = GetInt(step, "retryCount") ?? 0;
            var safeToRetry = type.ToLowerInvariant() is "navigate" or "waitforelement" or "assert" or "extract";
            if (retries is < 0 or > 3 || retries > 0 && !safeToRetry)
                throw new InvalidOperationException($"Step {id} 不支持该重试配置。");
            await report(new("Running", id, Math.Clamp((index * 100) / Math.Max(1, steps.Count), 0, 99), $"开始执行 {type}"));
            for (var attempt = 0; attempt <= retries; attempt++)
            {
                page.SetDefaultTimeout(timeoutMs);
                page.SetDefaultNavigationTimeout(timeoutMs);
                try
                {
            switch (type.ToLowerInvariant())
            {
                case "navigate": await page.GotoAsync(Resolve(GetString(config, "url") ?? throw new InvalidOperationException("Navigate 缺少 url。"), parameters)); break;
                case "click": await page.Locator(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Click 缺少 selector。"), parameters)).ClickAsync(); break;
                case "input": await page.Locator(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Input 缺少 selector。"), parameters)).FillAsync(Resolve(GetString(config, "value") ?? string.Empty, parameters)); break;
                case "select": await page.Locator(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Select 缺少 selector。"), parameters)).SelectOptionAsync(Resolve(GetString(config, "value") ?? string.Empty, parameters)); break;
                case "wait": await Task.Delay(Math.Clamp(GetInt(config, "milliseconds") ?? 500, 0, 120_000), cancellationToken)
                    .WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken); break;
                case "waitforelement": await page.Locator(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("WaitForElement 缺少 selector。"), parameters)).WaitForAsync(new LocatorWaitForOptions { Timeout = Math.Min(GetInt(config, "timeout") ?? timeoutMs, timeoutMs) }); break;
                case "screenshot":
                    var screenshotPath = Resolve(GetString(config, "path") ?? $"artifacts/{id}.png", parameters);
                    await SaveScreenshotAsync(page, screenshotPath, GetBool(config, "fullPage") ?? true);
                    await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), "已生成截图", await BuildArtifactAsync("Screenshot", screenshotPath, "image/png")));
                    break;
                case "download":
                    var downloadPath = Resolve(GetString(config, "path") ?? $"artifacts/{id}.download", parameters);
                    await ExecuteDownloadAsync(page, config, parameters, downloadPath, timeoutMs, cancellationToken);
                    await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), "已完成下载", await BuildArtifactAsync("Download", downloadPath, null)));
                    break;
                case "assert": await ExecuteAssertAsync(page, config, parameters); break;
                case "extract":
                    var value = await page.Locator(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Extract 缺少 selector。"), parameters)).InnerTextAsync();
                    await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), $"Extract: {value[..Math.Min(value.Length, 500)]}")); break;
                case "upload":
                    var uploadPath = Resolve(GetString(config, "path") ?? throw new InvalidOperationException("Upload 缺少 path。"), parameters);
                    await page.Locator(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Upload 缺少 selector。"), parameters)).SetInputFilesAsync(uploadPath);
                    if (File.Exists(uploadPath)) await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), "已完成文件上传", await BuildArtifactAsync("Upload", uploadPath, null)));
                    break;
                case "condition": await ExecuteConditionAsync(page, config, parameters, report, cancellationToken, depth); break;
                case "loop": await ExecuteLoopAsync(page, config, parameters, report, cancellationToken, depth); break;
                case "subworkflow":
                    var nested = config.TryGetProperty("steps", out var nestedSteps) && nestedSteps.ValueKind == JsonValueKind.Array ? nestedSteps.EnumerateArray().ToArray() : Array.Empty<JsonElement>();
                    await ExecuteStepsAsync(page, nested, parameters, report, cancellationToken, depth + 1); break;
                case "end": return;
                case "script": throw new InvalidOperationException("Script Step 默认被禁止，必须通过受控 Script Provider 执行。 ");
                case "humantask":
                    await report(new("WaitingForHuman", id, (index * 100) / Math.Max(1, steps.Count), "Workflow 等待人工介入。"));
                    break;
                default: throw new NotSupportedException($"NodeAgent 暂不支持 Workflow Step: {type}");
            }
                    break;
                }
                catch (Exception ex) when (attempt < retries && safeToRetry && !cancellationToken.IsCancellationRequested &&
                    (ex is PlaywrightException or TimeoutException || type.Equals("Assert", StringComparison.OrdinalIgnoreCase) && ex is InvalidOperationException))
                {
                    await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), $"步骤失败，准备第 {attempt + 1} 次重试。"));
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(2000, 250 * (attempt + 1))), cancellationToken);
                }
                finally
                {
                    page.SetDefaultTimeout(30_000);
                    page.SetDefaultNavigationTimeout(30_000);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            await report(new("Running", id, Math.Min(99, ((index + 1) * 100) / Math.Max(1, steps.Count)), $"完成 {type}"));
        }
    }

    private static async Task ExecuteConditionAsync(IPage page, JsonElement config, IReadOnlyDictionary<string, string?> parameters, Func<WorkflowRuntimeEvent, Task> report, CancellationToken cancellationToken, int depth)
    {
        var selector = Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Condition 缺少 selector。"), parameters);
        var actual = await page.Locator(selector).InnerTextAsync();
        var expected = Resolve(GetString(config, "contains") ?? string.Empty, parameters);
        var matched = actual.Contains(expected, StringComparison.Ordinal);
        var property = matched ? "then" : "else";
        if (config.TryGetProperty(property, out var branch) && branch.ValueKind == JsonValueKind.Array)
            await ExecuteStepsAsync(page, branch.EnumerateArray().ToArray(), parameters, report, cancellationToken, depth + 1);
    }

    private static async Task ExecuteLoopAsync(IPage page, JsonElement config, IReadOnlyDictionary<string, string?> parameters, Func<WorkflowRuntimeEvent, Task> report, CancellationToken cancellationToken, int depth)
    {
        var count = Math.Clamp(GetInt(config, "count") ?? 1, 0, 1000);
        var nested = config.TryGetProperty("steps", out var nestedSteps) && nestedSteps.ValueKind == JsonValueKind.Array ? nestedSteps.EnumerateArray().ToArray() : Array.Empty<JsonElement>();
        for (var i = 0; i < count; i++) { cancellationToken.ThrowIfCancellationRequested(); await ExecuteStepsAsync(page, nested, parameters, report, cancellationToken, depth + 1); }
    }

    private static async Task ExecuteDownloadAsync(IPage page, JsonElement config, IReadOnlyDictionary<string, string?> parameters, string target, int timeoutMs, CancellationToken cancellationToken)
    {
        var selector = Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Download 缺少 selector。"), parameters);
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var download = await page.RunAndWaitForDownloadAsync(() => page.Locator(selector).ClickAsync(), new PageRunAndWaitForDownloadOptions { Timeout = Math.Min(GetInt(config, "timeout") ?? timeoutMs, timeoutMs) });
        await download.SaveAsAsync(target);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task SaveScreenshotAsync(IPage page, string path, bool fullPage)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = fullPage });
    }

    private static async Task<WorkflowRuntimeArtifact?> BuildArtifactAsync(string type, string path, string? contentType)
    {
        if (!File.Exists(path)) return null;
        var info = new FileInfo(path);
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream);
        return new(type, info.Name, path, contentType, info.Length, Convert.ToHexString(hash).ToLowerInvariant());
    }

    private static async Task ExecuteAssertAsync(IPage page, JsonElement config, IReadOnlyDictionary<string, string?> parameters)
    {
        var selector = Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Assert 缺少 selector。"), parameters);
        var expected = Resolve(GetString(config, "contains") ?? string.Empty, parameters);
        var actual = await page.Locator(selector).InnerTextAsync();
        if (!actual.Contains(expected, StringComparison.Ordinal)) throw new InvalidOperationException($"Assert 失败：selector={selector}");
    }

    private static JsonElement[] GetSteps(JsonElement root) => root.TryGetProperty("steps", out var stepArray) && stepArray.ValueKind == JsonValueKind.Array ? stepArray.EnumerateArray().ToArray() : [];
    private static string? GetString(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int? GetInt(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : null;
    private static bool? GetBool(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
    private static string Resolve(string value, IReadOnlyDictionary<string, string?> parameters) { foreach (var item in parameters) value = value.Replace("{{" + item.Key + "}}", item.Value ?? string.Empty, StringComparison.Ordinal); return value; }
}
