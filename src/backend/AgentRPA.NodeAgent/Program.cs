using AgentRPA.NodeAgent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var serverUrl = builder.Configuration["NodeAgent:ServerUrl"] ?? "https://localhost:5001";
builder.Services.Configure<NodeAgentOptions>(builder.Configuration.GetSection("NodeAgent"));
builder.Services.AddHttpClient("AgentRPA.Server", client =>
{
    client.BaseAddress = new Uri(serverUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHostedService<NodeAgentWorker>();

await builder.Build().RunAsync();
