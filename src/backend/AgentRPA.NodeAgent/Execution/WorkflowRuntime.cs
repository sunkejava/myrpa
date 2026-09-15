using System.Text.Json;
using Microsoft.Playwright;

namespace AgentRPA.NodeAgent.Execution;

public interface IWorkflowRuntime
{
    Task ExecuteAsync(string definitionJson, IReadOnlyDictionary<string, string?> parameters, Func<WorkflowRuntimeEvent, Task> report, CancellationToken cancellationToken);
}

public sealed record WorkflowRuntimeEvent(string Status, string? StepId, int ProgressPercent, string? Message);

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
            await report(new("Running", id, Math.Clamp((index * 100) / Math.Max(1, steps.Count), 0, 99), $"开始执行 {type}"));
            switch (type.ToLowerInvariant())
            {
                case "navigate": await page.GotoAsync(Resolve(GetString(config, "url") ?? throw new InvalidOperationException("Navigate 缺少 url。"), parameters)); break;
                case "click": await page.Locator(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Click 缺少 selector。"), parameters)).ClickAsync(); break;
                case "input": await page.Locator(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Input 缺少 selector。"), parameters)).FillAsync(Resolve(GetString(config, "value") ?? string.Empty, parameters)); break;
                case "select": await page.Locator(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Select 缺少 selector。"), parameters)).SelectOptionAsync(Resolve(GetString(config, "value") ?? string.Empty, parameters)); break;
                case "wait": await page.WaitForTimeoutAsync(Math.Clamp(GetInt(config, "milliseconds") ?? 500, 0, 120_000)); break;
                case "waitforelement": await page.Locator(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("WaitForElement 缺少 selector。"), parameters)).WaitForAsync(new LocatorWaitForOptions { Timeout = GetInt(config, "timeout") ?? 30_000 }); break;
                case "screenshot": await SaveScreenshotAsync(page, config, parameters, id); break;
                case "download": await ExecuteDownloadAsync(page, config, parameters, id, cancellationToken); break;
                case "assert": await ExecuteAssertAsync(page, config, parameters); break;
                case "extract":
                    var value = await page.Locator(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Extract 缺少 selector。"), parameters)).InnerTextAsync();
                    await report(new("Running", id, (index * 100) / Math.Max(1, steps.Count), $"Extract: {value[..Math.Min(value.Length, 500)]}")); break;
                case "upload": await page.Locator(Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Upload 缺少 selector。"), parameters)).SetInputFilesAsync(Resolve(GetString(config, "path") ?? throw new InvalidOperationException("Upload 缺少 path。"), parameters)); break;
                case "condition": await ExecuteConditionAsync(page, config, parameters, report, cancellationToken, depth); break;
                case "loop": await ExecuteLoopAsync(page, config, parameters, report, cancellationToken, depth); break;
                case "subworkflow":
                    var nested = config.TryGetProperty("steps", out var nestedSteps) && nestedSteps.ValueKind == JsonValueKind.Array ? nestedSteps.EnumerateArray().ToArray() : Array.Empty<JsonElement>();
                    await ExecuteStepsAsync(page, nested, parameters, report, cancellationToken, depth + 1); break;
                case "end": return;
                case "script": throw new InvalidOperationException("Script Step 默认被禁止，必须通过受控 Script Provider 执行。 ");
                case "humantask": await report(new("WaitingForHuman", id, (index * 100) / Math.Max(1, steps.Count), "Workflow 等待人工介入。")); throw new InvalidOperationException("HumanTask 已进入人工介入状态，请由服务端恢复 Execution 后重新调度。 ");
                default: throw new NotSupportedException($"NodeAgent 暂不支持 Workflow Step: {type}");
            }
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
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteStepsAsync(page, nested, parameters, report, cancellationToken, depth + 1);
        }
    }

    private static async Task ExecuteDownloadAsync(IPage page, JsonElement config, IReadOnlyDictionary<string, string?> parameters, string id, CancellationToken cancellationToken)
    {
        var selector = Resolve(GetString(config, "selector") ?? throw new InvalidOperationException("Download 缺少 selector。"), parameters);
        var target = Resolve(GetString(config, "path") ?? $"artifacts/{id}.download", parameters);
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var download = await page.RunAndWaitForDownloadAsync(() => page.Locator(selector).ClickAsync(), new PageRunAndWaitForDownloadOptions { Timeout = GetInt(config, "timeout") ?? 30_000 });
        await download.SaveAsAsync(target);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task SaveScreenshotAsync(IPage page, JsonElement config, IReadOnlyDictionary<string, string?> parameters, string id)
    {
        var path = Resolve(GetString(config, "path") ?? $"artifacts/{id}.png", parameters);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = GetBool(config, "fullPage") ?? true });
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
    private static int? GetInt(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : null;
    private static bool? GetBool(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
    private static string Resolve(string value, IReadOnlyDictionary<string, string?> parameters)
    {
        foreach (var item in parameters) value = value.Replace("{{" + item.Key + "}}", item.Value ?? string.Empty, StringComparison.Ordinal);
        return value;
    }
}
