using AgentRPA.Api.HostedServices;
using AgentRPA.Api.Hubs;
using AgentRPA.Application.Nodes;
using AgentRPA.Application.Scheduling;
using AgentRPA.Infrastructure.Nodes;
using AgentRPA.Infrastructure.Persistence;
using AgentRPA.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddSingleton<NodeAgentConnectionRegistry>();
builder.Services.AddHostedService<NodeHealthMonitor>();

var connectionString = builder.Configuration.GetConnectionString("AgentRPA") ?? "Data Source=agentrpa.db";
builder.Services.AddDbContext<AgentRpaDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<INodeRegistryService, EfNodeRegistryService>();
builder.Services.AddScoped<IExecutionNodeRegistry>(sp => sp.GetRequiredService<INodeRegistryService>());
builder.Services.AddScoped<IExecutionLeaseService, EfExecutionLeaseService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgentRpaDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHub<NodeAgentHub>("/hubs/node-agent", options => options.AllowStatefulReconnects = true);

app.Run();
