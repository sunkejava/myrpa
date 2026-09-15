using AgentRPA.Domain.Audit;
using AgentRPA.Infrastructure.Persistence;

namespace AgentRPA.Api.Middleware;

/// <summary>统一记录变更类 API 审计事件；不读取请求 Body，避免密码、Token、Cookie、PIN 进入日志。</summary>
public sealed class AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AgentRpaDbContext db)
    {
        if (HttpMethods.IsGet(context.Request.Method) ||
            context.Request.Path.StartsWithSegments("/swagger") ||
            context.Request.Path.StartsWithSegments("/hubs"))
        {
            await next(context);
            return;
        }

        try
        {
            await next(context);
            var actor = context.User.Identity?.Name ?? "anonymous";
            var resource = context.Request.Path.Value ?? "/";
            var action = context.Request.Method;
            var result = context.Response.StatusCode is >= 200 and < 400 ? "Success" : "Failed";
            db.AuditEntries.Add(new AuditEntry(actor, action, resource, null, result, $"HTTP {context.Response.StatusCode}"));
            await db.SaveChangesAsync(context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "API request failed: {Method} {Path}", context.Request.Method, context.Request.Path);
            throw;
        }
    }
}
