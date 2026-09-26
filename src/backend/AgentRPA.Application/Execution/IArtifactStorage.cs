namespace AgentRPA.Application.Execution;

/// <summary>执行产物存储抽象。StorageKey 永远是受控逻辑键，不向客户端暴露物理路径。</summary>
public interface IArtifactStorage
{
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
    Task StoreAsync(string storageKey, Stream content, CancellationToken cancellationToken = default);
}
