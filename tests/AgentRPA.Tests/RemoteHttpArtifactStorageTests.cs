using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AgentRPA.Infrastructure.Execution;
using Microsoft.Extensions.Configuration;

namespace AgentRPA.Tests;

public sealed class RemoteHttpArtifactStorageTests
{
    private static RemoteHttpArtifactStorage Create(HttpMessageHandler handler) => new(new HttpClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AgentRPA:Artifacts:Remote:Endpoint"] = "https://artifacts.example.test/store/",
            ["AgentRPA:Artifacts:Remote:BearerToken"] = "test-secret"
        }).Build());

    [Fact]
    public async Task Round_trip_put_head_get_delete_uses_bearer_auth_and_fixed_origin()
    {
        byte[]? stored = null;
        var calls = new List<HttpMethod>();
        var handler = new Handler(async request =>
        {
            Assert.Equal("https://artifacts.example.test/store/executions/abc", request.RequestUri!.AbsoluteUri);
            Assert.Equal(new AuthenticationHeaderValue("Bearer", "test-secret"), request.Headers.Authorization);
            calls.Add(request.Method);
            if (request.Method == HttpMethod.Put)
            {
                stored = await request.Content!.ReadAsByteArrayAsync();
                return new HttpResponseMessage(HttpStatusCode.Created);
            }
            if (request.Method == HttpMethod.Delete) { stored = null; return new HttpResponseMessage(HttpStatusCode.NoContent); }
            if (stored is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(stored) };
        });
        var storage = Create(handler);
        var bytes = Encoding.UTF8.GetBytes("verified artifact");

        await storage.StoreAsync("executions/abc", new MemoryStream(bytes));
        Assert.True(await storage.ExistsAsync("executions/abc"));
        await using var content = await storage.OpenReadAsync("executions/abc");
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        Assert.Equal(bytes, buffer.ToArray());
        await storage.DeleteAsync("executions/abc");
        Assert.False(await storage.ExistsAsync("executions/abc"));
        Assert.Equal(new[] { HttpMethod.Put, HttpMethod.Head, HttpMethod.Get, HttpMethod.Delete, HttpMethod.Head }, calls);
    }

    [Theory]
    [InlineData("../private")]
    [InlineData("executions/%2e%2e/private")]
    [InlineData("https://other.example.test/file")]
    public async Task Unsafe_storage_key_is_rejected_before_network_request(string key)
    {
        var called = false;
        var storage = Create(new Handler(_ => { called = true; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.ExistsAsync(key));
        Assert.False(called);
    }

    [Fact]
    public async Task Gateway_redirect_is_not_accepted_as_success()
    {
        var storage = Create(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("https://untrusted.example.test/file") }
        })));
        await Assert.ThrowsAsync<HttpRequestException>(() => storage.ExistsAsync("executions/abc"));
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
