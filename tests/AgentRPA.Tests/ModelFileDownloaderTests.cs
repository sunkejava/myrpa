using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using AgentRPA.NodeAgent.Execution;

namespace AgentRPA.Tests;

public sealed class ModelFileDownloaderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "model-test-" + Guid.NewGuid().ToString("N"));
    private static readonly byte[] Model = "GGUF-test-model-content"u8.ToArray();
    private static string Hash => Convert.ToHexString(SHA256.HashData(Model)).ToLowerInvariant();
    private string Target => Path.Combine(root, "owner", "model", "master", "model.gguf");
    private ModelFileDownloader Create(Handler handler) => new(new HttpClient(handler), new() { CatalogBaseUrl = "http://localhost:9876", DownloadRoot = root });

    [Fact]
    public async Task Download_verifies_content_and_reuses_only_matching_complete_file()
    {
        var handler = new Handler(); var downloader = Create(handler);
        var messages = new List<string>();
        var receipt = await downloader.DownloadAsync("owner/model", "master", "model.gguf", 60, message => { messages.Add(message); return Task.CompletedTask; }, default);
        Assert.Equal(Model, await File.ReadAllBytesAsync(Target));
        Assert.Equal(Hash, receipt.Sha256); Assert.Equal(Model.Length, receipt.Bytes); Assert.False(receipt.Reused);
        Assert.False(File.Exists(Target + ".partial"));
        Assert.Contains(messages, x => x.Contains("SHA256"));
        var reused = await downloader.DownloadAsync("owner/model", "master", "model.gguf", 60, _ => Task.CompletedTask, default);
        Assert.True(reused.Reused); Assert.Equal(1, handler.Downloads);
    }

    [Fact]
    public async Task Interrupted_download_resumes_at_exact_range_and_preserves_original_bytes()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Target)!);
        await File.WriteAllBytesAsync(Target + ".partial", Model[..7]);
        await File.WriteAllTextAsync(Target + ".partial.sha256", Hash);
        var handler = new Handler();
        await Create(handler).DownloadAsync("owner/model", "master", "model.gguf", 60, _ => Task.CompletedTask, default);
        Assert.Equal(7, handler.LastRange); Assert.Equal(Model, await File.ReadAllBytesAsync(Target));
    }

    [Fact]
    public async Task Server_ignoring_range_restarts_instead_of_appending_duplicate_content()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Target)!);
        await File.WriteAllBytesAsync(Target + ".partial", Model[..7]);
        await File.WriteAllTextAsync(Target + ".partial.sha256", Hash);
        await Create(new Handler { IgnoreRange = true }).DownloadAsync("owner/model", "master", "model.gguf", 60, _ => Task.CompletedTask, default);
        Assert.Equal(Model, await File.ReadAllBytesAsync(Target));
    }

    [Fact]
    public async Task Changed_revision_refuses_to_splice_old_partial_file()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Target)!);
        await File.WriteAllBytesAsync(Target + ".partial", Model[..7]);
        await File.WriteAllTextAsync(Target + ".partial.sha256", new string('0', 64));
        var handler = new Handler();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).DownloadAsync("owner/model", "master", "model.gguf", 60, _ => Task.CompletedTask, default));
        Assert.Equal(0, handler.Downloads); Assert.Equal(Model[..7], await File.ReadAllBytesAsync(Target + ".partial"));
    }

    [Fact]
    public async Task Hash_mismatch_keeps_evidence_and_never_creates_final_model()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(new Handler { HashOverride = new string('0', 64) }).DownloadAsync("owner/model", "master", "model.gguf", 60, _ => Task.CompletedTask, default));
        Assert.False(File.Exists(Target)); Assert.Equal(Model, await File.ReadAllBytesAsync(Target + ".partial"));
    }

    [Fact]
    public async Task Html_error_disguised_as_model_is_rejected_even_with_matching_hash()
    {
        var html = "HTML-test-model-content"u8.ToArray();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(new Handler { Content = html }).DownloadAsync("owner/model", "master", "model.gguf", 60, _ => Task.CompletedTask, default));
        Assert.False(File.Exists(Target));
    }

    [Fact]
    public async Task Missing_remote_file_does_not_start_download()
    {
        var handler = new Handler { MissingFile = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).DownloadAsync("owner/model", "master", "model.gguf", 60, _ => Task.CompletedTask, default));
        Assert.Equal(0, handler.Downloads); Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task Size_limit_stops_before_transfer()
    {
        var handler = new Handler();
        var downloader = new ModelFileDownloader(new HttpClient(handler), new() { CatalogBaseUrl = "http://localhost", DownloadRoot = root, MaxFileBytes = 4 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.DownloadAsync("owner/model", "master", "model.gguf", 60, _ => Task.CompletedTask, default));
        Assert.Equal(0, handler.Downloads);
    }

    [Theory]
    [InlineData("owner/model", "master", "../model.gguf")]
    [InlineData("../model", "master", "model.gguf")]
    [InlineData("owner/model", "../master", "model.gguf")]
    [InlineData("owner/model", "master", "C:\\model.gguf")]
    [InlineData("owner/model", "master", "model.html")]
    public void Unsafe_identity_is_rejected(string repo, string revision, string filename) =>
        Assert.Throws<InvalidOperationException>(() => ModelFileDownloader.ValidateIdentity(repo, revision, filename));

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private sealed class Handler : HttpMessageHandler
    {
        public int Downloads { get; private set; }
        public long? LastRange { get; private set; }
        public bool IgnoreRange { get; init; }
        public bool MissingFile { get; init; }
        public string? HashOverride { get; init; }
        public byte[] Content { get; init; } = Model;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/files"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { Data = new { Files = MissingFile ? [] : new[] { new { Path = "model.gguf", Type = "file", Size = Content.Length, Sha256 = HashOverride ?? Convert.ToHexString(SHA256.HashData(Content)).ToLowerInvariant() } } } })) });
            Downloads++; LastRange = request.Headers.Range?.Ranges.Single().From;
            var offset = IgnoreRange ? 0 : (int)(LastRange ?? 0);
            var response = new HttpResponseMessage(offset == 0 ? HttpStatusCode.OK : HttpStatusCode.PartialContent) { Content = new ByteArrayContent(Content[offset..]) };
            if (offset > 0) response.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, Content.Length - 1, Content.Length);
            return Task.FromResult(response);
        }
    }
}
