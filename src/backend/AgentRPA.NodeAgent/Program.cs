using AgentRPA.NodeAgent;
using AgentRPA.NodeAgent.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AgentRPA.Contracts.Nodes;
using AgentRPA.Application.Abstractions;
using AgentRPA.Infrastructure.Hardware;

var builder = Host.CreateApplicationBuilder(args);
var serverUrl = builder.Configuration["NodeAgent:ServerUrl"] ?? "https://localhost:5001";
builder.Services.Configure<NodeAgentOptions>(builder.Configuration.GetSection("NodeAgent"));
var siteConfigs = builder.Configuration.GetSection("NodeAgent:SiteAdapters").Get<List<ConfiguredSiteAdapterOptions>>() ?? [];
var siteAdapters = siteConfigs.Select(x => new ConfiguredWorkflowSiteAdapter(x)).ToArray();
if (siteAdapters.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != siteAdapters.Length)
    throw new InvalidOperationException("节点站点 Adapter 编码不能重复。");
builder.Services.PostConfigure<NodeAgentOptions>(options =>
{
    foreach (var adapter in siteAdapters)
        if (!options.Capabilities.Any(x => string.Equals(x.Code, "Adapter:" + adapter.Code, StringComparison.OrdinalIgnoreCase)))
            options.Capabilities.Add(new NodeCapabilityDto("Adapter:" + adapter.Code));
});
builder.Services.AddHttpClient("AgentRPA.Server", client =>
{
    client.BaseAddress = new Uri(serverUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddSingleton<IWorkflowSiteAdapter, DirectWorkflowSiteAdapter>();
builder.Services.AddSingleton<IWorkflowSiteAdapter, QingdaoSocialSecuritySiteAdapter>();
foreach (var adapter in siteAdapters) builder.Services.AddSingleton<IWorkflowSiteAdapter>(adapter);
builder.Services.AddSingleton<IWorkflowRuntime, PlaywrightWorkflowRuntime>();
builder.Services.AddSingleton<IHardwareCredentialProvider, WindowsCertificateHardwareProvider>();
builder.Services.AddHostedService<NodeAgentWorker>();
await builder.Build().RunAsync();
