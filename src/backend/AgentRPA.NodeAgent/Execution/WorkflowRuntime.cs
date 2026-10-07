using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Playwright;
using AgentRPA.Application.Abstractions;

namespace AgentRPA.NodeAgent.Execution;

public interface IWorkflowRuntime
{
    Task ExecuteAsync(string definitionJson, IReadOnlyDictionary<string, string?> parameters, Func<WorkflowRuntimeEvent, Task<string?>> report, CancellationToken cancellationToken);
}

/// <summary>Workflow Runtime 事件；Artifact 仅携带受控本地产物的元数据，不携带文件内容。</summary>
public sealed record WorkflowRuntimeEvent(
    string Status,
    string? StepId,
    int ProgressPercent,
    string? Message,
    WorkflowRuntimeArtifact? Artifact = null,
    string? StepType = null,
    string? OutputKey = null,
    string? OutputValue = null,
    string? InterventionType = null,
    string? InterventionTitle = null);

public sealed record WorkflowRuntimeArtifact(
    string ArtifactType,
    string FileName,
    string StorageKey,
    string? ContentType,
    long Size,
    string? Hash);

/// <summary>基于 Playwright 的确定性浏览器 Workflow Runtime。</summary>
public sealed class PlaywrightWorkflowRuntime(IEnumerable<IWorkflowSiteAdapter> adapters, IHardwareCredentialProvider hardwareProvider, ICaptchaProvider? captchaProvider = null, ModelDownloadStep? modelDownload = null) : IWorkflowRuntime
{
    public async Task ExecuteAsync(string definitionJson, IReadOnlyDictionary<string, string?> parameters, Func<WorkflowRuntimeEvent, Task<string?>> report, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson);
        var root = document.RootElement;
        var steps = GetSteps(root);
        if (steps.Length == 0) { await report(new("Succeeded", null, 100, "Workflow 没有可执行步骤。")); return; }
        var adapterCode = GetString(root, "adapter") ?? "direct";
        var adapter = adapters.SingleOrDefault(x => string.Equals(x.Code, adapterCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotSupportedException($"NodeAgent 未安装 Workflow Adapter：{adapterCode}");

        var runDirectory = Path.GetFullPath(Path.Combine("artifacts", "runs", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(runDirectory);
        var logPath = Path.Combine(runDirectory, "execution.jsonl");
        var recordVideo = GetBool(root, "recordVideo") == true;
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true,
            RecordVideoDir = recordVideo ? Path.Combine(runDirectory, "videos") : null,
            RecordVideoSize = recordVideo ? new RecordVideoSize { Width = 1280, Height = 720 } : null,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });
        var page = await context.NewPageAsync();
        var variables = parameters.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var protectedNames = parameters.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        protectedNames.Add("systemBaseUrl");
        async Task AppendAsync(WorkflowRuntimeEvent e)
        {
            // 不保存参数、输入值或提取内容，只保存步骤、时间和已脱敏的诊断信息。
            var line = JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, e.Status, e.StepId, e.StepType,
                e.ProgressPercent, message = AgentRPA.Domain.Common.SensitiveTextSanitizer.Sanitize(e.Message),
                pageUrl = SafePageUrl(page.Url), artifact = e.Artifact?.FileName });
            await File.AppendAllTextAsync(logPath, line + Environment.NewLine);
        }
        async Task<string?> ReportAsync(WorkflowRuntimeEvent e)
        {
            await AppendAsync(e);
            return await report(e);
        }
        Exception? failure = null;
        try
        {
            await ExecuteStepsAsync(page, steps, variables, protectedNames, ReportAsync, cancellationToken, adapter, hardwareProvider, captchaProvider, modelDownload, 0);
            await AppendAsync(new("Succeeded", null, 100, "Workflow 执行完成。"));
        }
        catch (Exception ex)
        {
            failure = ex;
            await AppendAsync(new(cancellationToken.IsCancellationRequested ? "Cancelled" : "Failed", null, 0, ex.Message));
            if (!page.IsClosed)
            {
                var errorPath = Path.Combine(runDirectory, "failure.png");
                try
                {
                    await SaveScreenshotAsync(page, errorPath, true);
                    await report(new("Running", null, 0, "已保存失败现场截图。", await BuildArtifactAsync("Screenshot", errorPath, "image/png")));
                }
                catch { /* 浏览器崩溃时仍尝试保存其他证据。 */ }
            }
        }
        finally
        {
            await context.CloseAsync(); // 关闭 Context 后视频才完整落盘；终态在证据上传后上报。
        }
        if (recordVideo)
            foreach (var video in Directory.EnumerateFiles(Path.Combine(runDirectory, "videos"), "*.webm"))
                await report(new("Running", null, 99, "已保存浏览器执行视频。", await BuildArtifactAsync("Video", video, "video/webm")));
        await report(new("Running", null, 99, "已保存完整执行日志。", await BuildArtifactAsync("ExecutionLog", logPath, "application/x-ndjson")));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        await report(new("Succeeded", null, 100, "Workflow 执行完成，诊断证据已保存。"));
    }

    private static string SafePageUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"
        ? uri.GetLeftPart(UriPartial.Path) : "browser";

    private static async Task ExecuteStepsAsync(IPage page, IReadOnlyList<JsonElement> steps, IReadOnlyDictionary<string, string?> parameters, IReadOnlySet<string> protectedNames, Func<WorkflowRuntimeEvent, Task<string?>> report, CancellationToken cancellationToken, IWorkflowSiteAdapter adapter, IHardwareCredentialProvider hardwareProvider, ICaptchaProvider? captchaProvider, ModelDownloadStep? modelDownload, int depth)
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
            await report(new("StepStarted", id, Math.Clamp((index * 100) / Math.Max(1, steps.Count), 0, 99), $"开始执行 {type}", StepType: type));
            for (var attempt = 0; attempt <= retries; attempt++)
            {
                page.SetDefaultTimeout(timeoutMs);
                page.SetDefaultNavigationTimeout(timeoutMs);
                try
                {
            switch (type.ToLowerInvariant())
            {
                case "navigate": await page.GotoAsync(adapter.ResolveUrl(Resolve(GetString(config, "url") ?? throw new InvalidOperationException("Navigate 缺少 url。"), parameters), parameters)); break;
                case "click": await page.Locator(adapter.ResolveSelector(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Click 缺少 selector。"), parameters))).ClickAsync(); break;
                case "press": await page.Locator(adapter.ResolveSelector(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Press 缺少 selector。"), parameters))).PressAsync(GetString(config, "key") ?? "Enter"); break;
                case "modeldownload":
                    if (modelDownload is null) throw new InvalidOperationException("节点未配置模型下载服务。");
                    var receipt = await modelDownload.ExecuteAsync(page, config, parameters,
                        async message => { await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), message)); }, cancellationToken);
                    var receiptJson = JsonSerializer.Serialize(receipt);
                    var receiptPath = Path.Combine("artifacts", "model-download-" + Guid.NewGuid().ToString("N") + ".json");
                    Directory.CreateDirectory("artifacts");
                    await File.WriteAllTextAsync(receiptPath, receiptJson, cancellationToken);
                    await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), "模型保存在执行节点；已上传下载清单。",
                        await BuildArtifactAsync("DownloadManifest", receiptPath, "application/json"), OutputKey: "modelDownload", OutputValue: receiptJson));
                    break;
                case "input": await page.Locator(adapter.ResolveSelector(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Input 缺少 selector。"), parameters))).FillAsync(Resolve(GetString(config, "value") ?? string.Empty, parameters)); break;
                case "select": await page.Locator(adapter.ResolveSelector(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Select 缺少 selector。"), parameters))).SelectOptionAsync(Resolve(GetString(config, "value") ?? string.Empty, parameters)); break;
                case "wait": await Task.Delay(Math.Clamp(GetInt(config, "milliseconds") ?? 500, 0, 120_000), cancellationToken)
                    .WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken); break;
                case "waitforelement": await page.Locator(adapter.ResolveSelector(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("WaitForElement 缺少 selector。"), parameters))).WaitForAsync(new LocatorWaitForOptions { Timeout = Math.Min(GetInt(config, "timeout") ?? timeoutMs, timeoutMs) }); break;
                case "screenshot":
                    var screenshotPath = Resolve(GetString(config, "path") ?? $"artifacts/{id}.png", parameters);
                    await SaveScreenshotAsync(page, screenshotPath, GetBool(config, "fullPage") ?? true);
                    await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), "已生成截图", await BuildArtifactAsync("Screenshot", screenshotPath, "image/png")));
                    break;
                case "download":
                    var downloadPath = Resolve(GetString(config, "path") ?? $"artifacts/{id}.download", parameters);
                    await ExecuteDownloadAsync(page, config, parameters, downloadPath, timeoutMs, cancellationToken, adapter);
                    await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), "已完成下载", await BuildArtifactAsync("Download", downloadPath, null)));
                    break;
                case "assert": await ExecuteAssertAsync(page, config, parameters, adapter); break;
                case "extract":
                    var value = await page.Locator(adapter.ResolveSelector(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Extract 缺少 selector。"), parameters))).InnerTextAsync();
                    var outputKey = GetString(config, "output") ?? throw new InvalidOperationException("Extract 缺少 config.output。");
                    if (protectedNames.Contains(outputKey)) throw new InvalidOperationException("提取结果不能覆盖任务参数或系统地址。");
                    if (value.Length > 16_384) throw new InvalidOperationException("提取结果超过 16384 字符限制。");
                    if (parameters is not Dictionary<string, string?> variables) throw new InvalidOperationException("Workflow 变量上下文无效。");
                    variables[outputKey] = value;
                    await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), "已提取结构化字段", OutputKey: outputKey, OutputValue: value)); break;
                case "upload":
                    var uploadPath = Resolve(GetString(config, "path") ?? throw new InvalidOperationException("Upload 缺少 path。"), parameters);
                    await page.Locator(adapter.ResolveSelector(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Upload 缺少 selector。"), parameters))).SetInputFilesAsync(uploadPath);
                    if (File.Exists(uploadPath)) await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), "已完成文件上传", await BuildArtifactAsync("Upload", uploadPath, null)));
                    break;
                case "condition": await ExecuteConditionAsync(page, config, parameters, protectedNames, report, cancellationToken, adapter, hardwareProvider, captchaProvider, modelDownload, depth); break;
                case "loop": await ExecuteLoopAsync(page, config, parameters, protectedNames, report, cancellationToken, adapter, hardwareProvider, captchaProvider, modelDownload, depth); break;
                case "subworkflow":
                    var nested = config.TryGetProperty("steps", out var nestedSteps) && nestedSteps.ValueKind == JsonValueKind.Array ? nestedSteps.EnumerateArray().ToArray() : Array.Empty<JsonElement>();
                    await ExecuteStepsAsync(page, nested, parameters, protectedNames, report, cancellationToken, adapter, hardwareProvider, captchaProvider, modelDownload, depth + 1); break;
                case "end": return;
                case "script": throw new InvalidOperationException("Script Step 默认被禁止，必须通过受控 Script Provider 执行。 ");
                case "humantask":
                    var interventionType = GetString(config, "interventionType") ?? "ManualApproval";
                    if (interventionType.Equals("QrLogin", StringComparison.OrdinalIgnoreCase))
                    {
                        var qrSelector = adapter.ResolveSelector(Resolve(GetString(config, "qrSelector") ?? throw new InvalidOperationException("扫码登录缺少 qrSelector。"), parameters));
                        var qrPath = Path.Combine("artifacts", $"qr-{Guid.NewGuid():N}.png");
                        Directory.CreateDirectory("artifacts");
                        await page.Locator(qrSelector).ScreenshotAsync(new LocatorScreenshotOptions { Path = qrPath });
                        await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), "登录二维码已转发至执行产物。", await BuildArtifactAsync("QrLogin", qrPath, "image/png")));
                    }
                    if (interventionType.Equals("Captcha", StringComparison.OrdinalIgnoreCase) && GetString(config, "imageSelector") is { } imageSelector)
                    {
                        var captchaPath = Path.Combine("artifacts", $"captcha-{Guid.NewGuid():N}.png");
                        Directory.CreateDirectory("artifacts");
                        await page.Locator(adapter.ResolveSelector(Resolve(imageSelector, parameters))).ScreenshotAsync(new LocatorScreenshotOptions { Path = captchaPath });
                        await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), "验证码图片已转发至执行产物。", await BuildArtifactAsync("Captcha", captchaPath, "image/png")));
                        if (GetBool(config, "autoRecognize") == true && captchaProvider is not null)
                        {
                            var recognized = await captchaProvider.RecognizeAsync(new CaptchaRequest(id, "image", await File.ReadAllBytesAsync(captchaPath, cancellationToken)), cancellationToken);
                            if (recognized.Success && recognized.Value is { Length: > 0 and <= 32 } code && !code.Any(char.IsControl))
                            {
                                await page.Locator(adapter.ResolveSelector(Resolve(GetString(config, "inputSelector") ?? throw new InvalidOperationException("验证码缺少 inputSelector。"), parameters))).FillAsync(code);
                                await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), "验证码已自动识别并回填。"));
                                break;
                            }
                        }
                    }
                    var suppliedCode = await report(new("WaitingForHuman", id, (index * 100) / Math.Max(1, steps.Count), "Workflow 等待人工介入。",
                        InterventionType: interventionType,
                        InterventionTitle: GetString(config, "title") ?? $"流程步骤 {id} 等待人工确认"));
                    if (interventionType.Equals("Captcha", StringComparison.OrdinalIgnoreCase) || interventionType.Equals("SmsCode", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(suppliedCode)) throw new InvalidOperationException("验证码未提供，流程无法继续。");
                        await page.Locator(adapter.ResolveSelector(Resolve(GetString(config, "inputSelector") ?? throw new InvalidOperationException("验证码缺少 inputSelector。"), parameters))).FillAsync(suppliedCode);
                    }
                    if (interventionType.Equals("QrLogin", StringComparison.OrdinalIgnoreCase))
                        await page.Locator(adapter.ResolveSelector(Resolve(GetString(config, "successSelector") ?? throw new InvalidOperationException("扫码登录缺少 successSelector。"), parameters))).WaitForAsync(new LocatorWaitForOptions { Timeout = timeoutMs });
                    break;
                case "ukeysign":
                    await report(new("WaitingForHuman", id, (index * 100) / Math.Max(1, steps.Count), "等待用户确认本次证书签名。",
                        InterventionType: "UKeyConfirmation", InterventionTitle: $"确认步骤 {id} 的证书签名请求"));
                    cancellationToken.ThrowIfCancellationRequested();
                    var thumbprint = GetString(config, "certificateThumbprint") ?? throw new InvalidOperationException("UKeySign 缺少证书指纹。");
                    var digestSelector = adapter.ResolveSelector(Resolve(GetString(config, "digestSelector") ?? throw new InvalidOperationException("UKeySign 缺少摘要选择器。"), parameters));
                    var signatureSelector = adapter.ResolveSelector(Resolve(GetString(config, "signatureSelector") ?? throw new InvalidOperationException("UKeySign 缺少签名输入框选择器。"), parameters));
                    var digestBase64 = (await page.Locator(digestSelector).InnerTextAsync()).Trim();
                    var signed = await hardwareProvider.ExecuteAsync(new HardwareOperationRequest(id, "SignDigestSha256",
                        new Dictionary<string, object?> { ["certificateThumbprint"] = thumbprint, ["digestBase64"] = digestBase64 }), cancellationToken);
                    if (!signed.Success || string.IsNullOrWhiteSpace(signed.SignatureBase64))
                        throw new InvalidOperationException($"证书签名失败：{signed.ErrorCode ?? "UNKNOWN"}");
                    await page.Locator(signatureSelector).FillAsync(signed.SignatureBase64);
                    await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), "证书签名已填入页面。"));
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
            await report(new("StepCompleted", id, Math.Min(99, ((index + 1) * 100) / Math.Max(1, steps.Count)), $"完成 {type}", StepType: type));
        }
    }

    private static async Task ExecuteConditionAsync(IPage page, JsonElement config, IReadOnlyDictionary<string, string?> parameters, IReadOnlySet<string> protectedNames, Func<WorkflowRuntimeEvent, Task<string?>> report, CancellationToken cancellationToken, IWorkflowSiteAdapter adapter, IHardwareCredentialProvider hardwareProvider, ICaptchaProvider? captchaProvider, ModelDownloadStep? modelDownload, int depth)
    {
        var selector = adapter.ResolveSelector(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Condition 缺少 selector。"), parameters));
        var actual = await page.Locator(selector).InnerTextAsync();
        var expected = Resolve(GetString(config, "contains") ?? string.Empty, parameters);
        var matched = actual.Contains(expected, StringComparison.Ordinal);
        var property = matched ? "then" : "else";
        if (config.TryGetProperty(property, out var branch) && branch.ValueKind == JsonValueKind.Array)
            await ExecuteStepsAsync(page, branch.EnumerateArray().ToArray(), parameters, protectedNames, report, cancellationToken, adapter, hardwareProvider, captchaProvider, modelDownload, depth + 1);
    }

    private static async Task ExecuteLoopAsync(IPage page, JsonElement config, IReadOnlyDictionary<string, string?> parameters, IReadOnlySet<string> protectedNames, Func<WorkflowRuntimeEvent, Task<string?>> report, CancellationToken cancellationToken, IWorkflowSiteAdapter adapter, IHardwareCredentialProvider hardwareProvider, ICaptchaProvider? captchaProvider, ModelDownloadStep? modelDownload, int depth)
    {
        var count = Math.Clamp(GetInt(config, "count") ?? 1, 0, 1000);
        var nested = config.TryGetProperty("steps", out var nestedSteps) && nestedSteps.ValueKind == JsonValueKind.Array ? nestedSteps.EnumerateArray().ToArray() : Array.Empty<JsonElement>();
        for (var i = 0; i < count; i++) { cancellationToken.ThrowIfCancellationRequested(); await ExecuteStepsAsync(page, nested, parameters, protectedNames, report, cancellationToken, adapter, hardwareProvider, captchaProvider, modelDownload, depth + 1); }
    }

    private static async Task ExecuteDownloadAsync(IPage page, JsonElement config, IReadOnlyDictionary<string, string?> parameters, string target, int timeoutMs, CancellationToken cancellationToken, IWorkflowSiteAdapter adapter)
    {
        var selector = adapter.ResolveSelector(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Download 缺少 selector。"), parameters));
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

    private static async Task ExecuteAssertAsync(IPage page, JsonElement config, IReadOnlyDictionary<string, string?> parameters, IWorkflowSiteAdapter adapter)
    {
        var selector = adapter.ResolveSelector(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Assert 缺少 selector。"), parameters));
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
