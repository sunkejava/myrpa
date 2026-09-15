using AgentRPA.Api.HostedServices;
using AgentRPA.Api.Hubs;
using AgentRPA.Application.Nodes;
using AgentRPA.Application.Scheduling;
using AgentRPA.Infrastructure.Nodes;
using AgentRPA.Infrastructure.Persistence;
using AgentRPA.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance = context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddSignalR();
builder.Services.AddSingleton<NodeAgentConnectionRegistry>();
builder.Services.AddHostedService<NodeHealthMonitor>();
builder.Services.AddHostedService<ExecutionQueueWorker>();

var connectionString = builder.Configuration.GetConnectionString("AgentRPA") ?? "Data Source=agentrpa.db";
builder.Services.AddDbContext<AgentRpaDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<INodeRegistryService, EfNodeRegistryService>();
builder.Services.AddScoped<IExecutionNodeRegistry>(sp => sp.GetRequiredService<INodeRegistryService>());
builder.Services.AddScoped<IExecutionLeaseService, EfExecutionLeaseService>();
builder.Services.AddScoped<IExecutionScheduler, CapabilityExecutionScheduler>();

var app = builder.Build();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (exception is not null) app.Logger.LogError(exception, "Unhandled API exception");
    await Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "AgentRPA 服务端处理失败", detail: app.Environment.IsDevelopment() ? exception?.Message : null,
        extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
}));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgentRpaDbContext>();
    await db.Database.EnsureCreatedAsync();
}
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.MapControllers();
app.MapHub<NodeAgentHub>("/hubs/node-agent", options => options.AllowStatefulReconnects = true);
app.Run();
