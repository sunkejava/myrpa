using System.Net;
using System.Net.Http.Headers;
using AgentRPA.Application.Execution;
using Microsoft.Extensions.Configuration;

namespace AgentRPA.Infrastructure.Execution;

/// <summary>远程产物网关：受控 StorageKey 映射到固定 HTTPS 根地址，由网关负责持久化和访问控制。</summary>
public sealed class RemoteHttpArtifactStorage : IArtifactStorage
{
    private const long MaxArtifactSize = 50L * 1024 * 1024;
    private readonly HttpClient client;
    private readonly Uri root;
    private readonly string token;

    public RemoteHttpArtifactStorage(HttpClient client, IConfiguration configuration)
    {
        var endpoint = configuration["AgentRPA:Artifacts:Remote:Endpoint"];
        token = configuration["AgentRPA:Artifacts:Remote:BearerToken"] ?? string.Empty;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !uri.AbsolutePath.EndsWith('/'))
            throw new InvalidOperationException("远程产物 Endpoint 必须是以 / 结尾且不含凭据的 HTTPS 地址。");
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("远程产物 BearerToken 未配置。");
        this.client = client;
        root = uri;
    }

    public async Task StoreAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Put, storageKey);
        request.Content = new StreamContent(content, 64 * 1024);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        if (content.CanSeek) request.Content.Headers.ContentLength = content.Length - content.Position;
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, storageKey);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new FileNotFoundException("远程产物不存在。");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxArtifactSize) throw new InvalidOperationException("远程产物超过下载大小限制。");
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) != 0)
            {
                if (output.Length + read > MaxArtifactSize) throw new InvalidOperationException("远程产物超过下载大小限制。");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            output.Position = 0;
            return output;
        }
        catch { await output.DisposeAsync(); throw; }
    }

    public async Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Head, storageKey);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Delete, storageKey);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound) response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage Request(HttpMethod method, string storageKey)
    {
        var segments = storageKey.Split('/');
        if (segments.Length < 2 || segments.Any(x => x is "" or "." or ".." || x.Length > 128 ||
            x.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.'))))
            throw new InvalidOperationException("远程产物 StorageKey 无效。");
        var path = string.Join('/', segments.Select(Uri.EscapeDataString));
        var request = new HttpRequestMessage(method, new Uri(root, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
