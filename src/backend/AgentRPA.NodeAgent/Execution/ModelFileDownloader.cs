using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentRPA.NodeAgent.Execution;

public sealed class ModelDownloadOptions
{
    public string CatalogBaseUrl { get; set; } = "https://modelscope.cn";
    public string DownloadRoot { get; set; } = "downloads";
    public long MaxFileBytes { get; set; } = 100L * 1024 * 1024 * 1024;
}

public sealed record ModelDownloadReceipt(string Repository, string Revision, string FileName, string LocalPath, long Bytes, string Sha256, bool Reused);

/// <summary>流式下载大型 GGUF，使用远端大小/摘要检查完整性，断点文件与最终文件分开保存。</summary>
public sealed class ModelFileDownloader(HttpClient client, ModelDownloadOptions options)
{
    public async Task<ModelDownloadReceipt> DownloadAsync(string repository, string revision, string filePath, int timeoutSeconds,
        Func<string, Task> progress, CancellationToken cancellationToken)
    {
        ValidateIdentity(repository, revision, filePath);
        if (timeoutSeconds is < 60 or > 86_400) throw new InvalidOperationException("下载超时必须在 60 至 86400 秒之间。");
        var baseUri = new Uri(options.CatalogBaseUrl.TrimEnd('/') + "/");
        if (baseUri.Scheme != "https" && !(baseUri.Scheme == "http" && baseUri.IsLoopback))
            throw new InvalidOperationException("模型站点必须是 HTTPS 或本机测试站点。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var ct = timeout.Token;
        var apiPath = "api/v1/models/" + repository;
        using var metadataResponse = await client.GetAsync(new Uri(baseUri, apiPath + "/repo/files?Revision=" + Uri.EscapeDataString(revision) + "&Recursive=true"), ct);
        metadataResponse.EnsureSuccessStatusCode();
        using var metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync(ct));
        var files = metadata.RootElement.GetProperty("Data").GetProperty("Files").EnumerateArray();
        var matches = files.Where(x => x.TryGetProperty("Path", out var path) && path.GetString() == filePath &&
            (!x.TryGetProperty("Type", out var type) || type.GetString() != "tree")).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("指定版本中未找到唯一匹配的 GGUF 文件：" + filePath);
        var entry = matches[0];
        var size = entry.GetProperty("Size").GetInt64();
        var expectedHash = entry.TryGetProperty("Sha256", out var sha) ? sha.GetString() : null;
        if (size < 4 || size > options.MaxFileBytes) throw new InvalidOperationException("GGUF 文件大小超过节点限制或无效。");
        if (expectedHash is not { Length: 64 } || !expectedHash.All(Uri.IsHexDigit))
            throw new InvalidOperationException("远端缺少有效 SHA256，无法确认模型文件完整性。");
        var root = Path.GetFullPath(options.DownloadRoot);
        var target = Path.Combine(root, repository.Replace('/', Path.DirectorySeparatorChar), revision, filePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target) && new FileInfo(target).Length == size)
        {
            await progress("发现已下载文件，正在核对 SHA256。");
            if (await VerifyAsync(target, expectedHash, progress, ct))
                return new(repository, revision, filePath, target, size, expectedHash.ToLowerInvariant(), true);
            throw new InvalidOperationException("已存在同名文件但摘要不匹配；请移走旧文件后重试，避免覆盖用户数据。");
        }
        if (File.Exists(target)) throw new InvalidOperationException("最终文件已存在且大小不同，请移走后重试。");
        var partial = target + ".partial";
        var identityPath = partial + ".sha256";
        // 只有同一远端内容的部分文件才能恢复，master 更新后不得拼接两种版本。
        if (File.Exists(partial) && (!File.Exists(identityPath) || (await File.ReadAllTextAsync(identityPath, ct)).Trim() != expectedHash))
            throw new InvalidOperationException("断点文件对应不同模型版本，请移走 .partial 与 .sha256 后重试。");
        await File.WriteAllTextAsync(identityPath, expectedHash, ct);
        var offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        if (offset > size) throw new InvalidOperationException("断点文件长度异常。");
        if (offset < size)
        {
            await progress($"开始下载 {filePath}，总大小 {size} 字节，断点 {offset} 字节。");
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, apiPath + "/repo?Revision=" + Uri.EscapeDataString(revision) + "&FilePath=" + Uri.EscapeDataString(filePath)));
            if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                if (response.Content.Headers.ContentRange?.From != offset || response.Content.Headers.ContentRange?.Length != size)
                    throw new InvalidOperationException("远端断点响应范围不匹配。");
            }
            else if (response.StatusCode == HttpStatusCode.OK) offset = 0;
            else
                throw new InvalidOperationException("远端下载响应不支持。");
            var remaining = size - offset;
            if (response.Content.Headers.ContentLength is { } length && length != remaining)
                throw new InvalidOperationException("远端响应长度不匹配。");
            var drive = new DriveInfo(Path.GetPathRoot(target)!);
            if (drive.AvailableFreeSpace < remaining + 256L * 1024 * 1024) throw new IOException("节点磁盘剩余空间不足。");
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = new FileStream(partial, offset > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);
            var buffer = new byte[1024 * 1024];
            var downloaded = offset;
            var timer = Stopwatch.StartNew();
            while (true)
            {
                var count = await input.ReadAsync(buffer, ct);
                if (count == 0) break;
                if (downloaded + count > size) throw new IOException("远端数据超过声明大小。");
                await output.WriteAsync(buffer.AsMemory(0, count), ct);
                downloaded += count;
                if (timer.Elapsed >= TimeSpan.FromSeconds(10))
                {
                    await progress($"下载进度 {downloaded}/{size} 字节（{downloaded * 100d / size:F1}%）。");
                    timer.Restart();
                }
            }
            await output.FlushAsync(ct);
            if (downloaded != size) throw new IOException("下载中断；保留断点文件，可再次运行同一条数据恢复。");
        }
        await progress("下载接收完成，正在校验 GGUF 文件头及 SHA256。");
        if (!await VerifyAsync(partial, expectedHash, progress, ct))
            throw new InvalidOperationException("GGUF 文件头或 SHA256 校验失败，保留断点文件供排查。");
        File.Move(partial, target, overwrite: false);
        File.Delete(identityPath);
        await progress($"下载完成：{target}，SHA256={expectedHash.ToLowerInvariant()}。");
        return new(repository, revision, filePath, target, size, expectedHash.ToLowerInvariant(), false);
    }

    public static void ValidateIdentity(string repository, string revision, string filePath)
    {
        static bool Segment(string value) => Regex.IsMatch(value, @"^[A-Za-z0-9][A-Za-z0-9_.-]{0,199}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        var repoParts = repository.Split('/');
        if (repoParts.Length != 2 || !repoParts.All(Segment) || !Segment(revision) ||
            !filePath.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) || filePath.Split('/').Any(x => !Segment(x)))
            throw new InvalidOperationException("仓库必须为 owner/name，版本和 GGUF 路径不得包含目录穿越或特殊字符。");
    }

    private static async Task<bool> VerifyAsync(string path, string expectedHash, Func<string, Task> progress, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        var header = new byte[4];
        if (await stream.ReadAsync(header, ct) != 4 || !header.SequenceEqual("GGUF"u8.ToArray())) return false;
        stream.Position = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024];
        var timer = Stopwatch.StartNew();
        int count;
        while ((count = await stream.ReadAsync(buffer, ct)) > 0)
        {
            hash.AppendData(buffer, 0, count);
            if (timer.Elapsed >= TimeSpan.FromSeconds(10)) { await progress($"SHA256 校验进度 {stream.Position}/{stream.Length} 字节。"); timer.Restart(); }
        }
        return Convert.ToHexString(hash.GetHashAndReset()).Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
