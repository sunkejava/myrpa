using System.Security.Cryptography;
using System.Text;
using AgentRPA.Api.HostedServices;
using AgentRPA.Api.Hubs;
using AgentRPA.Api.Middleware;
using AgentRPA.Api.Mock;
using AgentRPA.Application.Agent;
using AgentRPA.Application.Batch;
using AgentRPA.Application.Execution;
using AgentRPA.Application.Identity;
using AgentRPA.Application.Nodes;
using AgentRPA.Application.Permission;
using AgentRPA.Application.Scheduling;
using AgentRPA.Application.Workflow;
using AgentRPA.Infrastructure.Agent;
using AgentRPA.Infrastructure.Execution;
using AgentRPA.Infrastructure.Identity;
using AgentRPA.Infrastructure.Nodes;
using AgentRPA.Infrastructure.Permission;
using AgentRPA.Infrastructure.Persistence;
using AgentRPA.Infrastructure.Scheduling;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance = context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
var jwt = builder.Configuration.GetSection("AgentRPA:Jwt");
var signingKey = jwt["SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey))
{
    if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("生产环境必须配置 AgentRPA:Jwt:SigningKey，且至少 32 个字符。");
    signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
else if (signingKey.Length < 32) throw new InvalidOperationException("AgentRPA:Jwt:SigningKey 必须至少 32 个字符。");
// 开发环境生成的临时密钥必须同时用于 JWT 签发与校验。
builder.Configuration["AgentRPA:Jwt:SigningKey"] = signingKey;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt["Issuer"], ValidateAudience = true, ValidAudience = jwt["Audience"], ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = System.Security.Claims.ClaimTypes.Name, RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSignalR(options => options.EnableDetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddSingleton<NodeAgentConnectionRegistry>();
builder.Services.AddHostedService<NodeHealthMonitor>();
builder.Services.AddHostedService<ExecutionQueueWorker>();
var connectionString = builder.Configuration.GetConnectionString("AgentRPA") ?? "Data Source=agentrpa.db";
builder.Services.AddDbContext<AgentRpaDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<INodeRegistryService, EfNodeRegistryService>();
builder.Services.AddScoped<IExecutionNodeRegistry>(sp => sp.GetRequiredService<INodeRegistryService>());
builder.Services.AddScoped<IExecutionLeaseService, EfExecutionLeaseService>();
builder.Services.AddScoped<IExecutionScheduler, CapabilityExecutionScheduler>();
builder.Services.AddSingleton<WorkflowParameterSchemaValidator>();
builder.Services.AddSingleton<WorkflowDefinitionValidator>();
builder.Services.AddSingleton<ISpreadsheetImportService, SpreadsheetImportService>();
builder.Services.AddScoped<IAgentResourceCatalog, EfAgentResourceCatalog>();
builder.Services.AddScoped<IAgentWorkflowResolver, EfAgentWorkflowResolver>();
builder.Services.AddScoped<AgentPlanningService>();
builder.Services.AddScoped<IAccessPolicyRepository, EfAccessPolicyRepository>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<WorkflowPermissionPreflight>();
builder.Services.AddScoped<PermissionManagementService>();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IIdentityService, EfIdentityService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.Configure<LlmProviderOptions>(builder.Configuration.GetSection("AgentRPA:Llm"));
builder.Services.AddHttpClient("llm", (sp, client) => client.Timeout = sp.GetRequiredService<IOptions<LlmProviderOptions>>().Value.Timeout);
builder.Services.AddScoped<ILlmProvider, OpenAiCompatibleLlmProvider>();
builder.Services.AddScoped<ILlmUsageRecorder, EfLlmUsageRecorder>();
builder.Services.AddSingleton<IArtifactStorage, LocalArtifactStorage>();
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
    await db.Database.MigrateAsync();
    await IdentityBootstrapper.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), app.Configuration, app.Environment.IsDevelopment());
    if (args.Contains("--seed-only", StringComparer.Ordinal))
    {
        if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("演示数据只允许在 Development 环境初始化。");
        await AgentRPA.Api.Seeding.DevelopmentSeedData.SeedAsync(db);
    }
}
if (args.Contains("--seed-only", StringComparer.Ordinal)) return;
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditMiddleware>();
app.MapControllers();
if (app.Environment.IsDevelopment()) app.MapMockSocialSecurity();
app.MapHub<NodeAgentHub>("/hubs/node-agent", options => options.AllowStatefulReconnects = true);
app.Run();
