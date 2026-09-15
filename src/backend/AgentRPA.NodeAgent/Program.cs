using AgentRPA.NodeAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<NodeAgentOptions>(builder.Configuration.GetSection("NodeAgent"));
builder.Services.AddHttpClient("AgentRPA.Server", (client, services) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<NodeAgentOptions>>().Value;
    client.BaseAddress = new Uri(options.ServerUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHostedService<NodeAgentWorker>();

await builder.Build().RunAsync();
