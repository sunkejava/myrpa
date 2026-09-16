using AgentRPA.Application.Execution;
using Microsoft.Extensions.Configuration;

namespace AgentRPA.Infrastructure.Execution;

/// <summary>本地执行产物存储。路径由受控根目录 + StorageKey 解析，禁止目录穿越。</summary>
public sealed class LocalArtifactStorage : IArtifactStorage
{
    private readonly string _root;

    public LocalArtifactStorage(IConfiguration configuration)
    {
        var configured = configuration["AgentRPA:Artifacts:Root"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "artifacts")
            : configured);
        Directory.CreateDirectory(_root);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(Resolve(storageKey)));

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(Resolve(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("StorageKey 不能为空。", nameof(storageKey));
        var normalized = storageKey.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("../", StringComparison.Ordinal) || normalized.Equals("..", StringComparison.Ordinal) || Path.IsPathRooted(normalized))
            throw new InvalidOperationException("非法 StorageKey。");

        var full = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("非法 StorageKey。");
        return full;
    }
}
