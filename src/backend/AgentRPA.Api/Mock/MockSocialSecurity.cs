using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AgentRPA.Domain.Mock;
using AgentRPA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentRPA.Api.Mock;

// This isolated fixture is deliberately registered only in Development. It has no production routes.
public static class MockSocialSecurity
{
    private const string Root = "/mock/qd-social-security";
    private const string SessionCookie = "qd-social-session";
    // Restart invalidates login sessions; employee data and completed receipts live in the database.
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Sessions = new();

    public static void MapMockSocialSecurity(this WebApplication app)
    {
        app.MapGet(Root, (HttpContext context) => Page(context, "登录", """
            <form method="post" action="/mock/qd-social-security/login">
              <label>账号 <input name="username" autocomplete="username" required></label>
              <label>密码 <input name="password" type="password" autocomplete="current-password" required></label>
              <button type="submit">登录</button>
            </form>
            """));

        app.MapPost(Root + "/login", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            if (form["username"] != "demo" || form["password"] != "Demo123!")
                return Page(context, "登录失败", "<p role='alert'>账号或密码错误</p><a href='/mock/qd-social-security'>重试</a>", 401);
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            Sessions[token] = DateTimeOffset.UtcNow.AddHours(1);
            context.Response.Cookies.Append(SessionCookie, token, new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = context.Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddHours(1), Path = Root
            });
            return Results.Redirect(Root + "/employees");
        });

        app.MapGet(Root + "/employees", (HttpContext context) =>
        {
            if (!Authenticated(context)) return Results.Redirect(Root);
            return Page(context, "青岛社保增减员", """
                <form method="post" action="/mock/qd-social-security/employees">
                  <label>业务类型 <select name="operation"><option value="add">增员</option><option value="remove">减员</option></select></label>
                  <label>姓名 <input name="employeeName" required></label>
                  <label>身份证号 <input name="idNumber" required></label>
                  <label>业务请求号 <input name="submissionId" placeholder="自动化任务请填写唯一请求号"></label>
                  <button type="submit">提交申报</button>
                </form>
                """);
        });

        // The read-only reconciliation endpoint reflects the mock site's actual employee registry.
        app.MapGet(Root + "/employees/status", async (HttpContext context, string idNumber, AgentRpaDbContext db) =>
        {
            if (!Authenticated(context)) return Results.Unauthorized();
            var id = idNumber.Trim().ToUpperInvariant();
            if (!ValidId(id)) return Results.BadRequest(new { message = "身份证号无效" });
            var name = await db.MockSocialEmployees.Where(x => x.IdNumber == id).Select(x => x.Name).SingleOrDefaultAsync();
            return Results.Ok(new { active = name is not null, employeeName = name });
        });

        app.MapPost(Root + "/employees", async (HttpContext context, AgentRpaDbContext db) =>
        {
            if (!Authenticated(context)) return Results.Redirect(Root);
            var form = await context.Request.ReadFormAsync();
            var name = form["employeeName"].ToString().Trim();
            var id = form["idNumber"].ToString().Trim().ToUpperInvariant();
            var operation = form["operation"].ToString();
            var submissionId = form["submissionId"].ToString().Trim();
            if (name.Length is < 2 or > 40 || !ValidId(id) || operation is not ("add" or "remove") ||
                submissionId.Length > 0 && !Regex.IsMatch(submissionId, @"^[A-Za-z0-9_-]{6,128}$", RegexOptions.CultureInvariant))
                return Result(context, false, "姓名、身份证号或业务类型无效", 400);
            // A transaction keeps the employee transition and its receipt together across process restarts.
            // SQLite serializes writers; primary keys reject duplicate requests from another API process.
            await using var transaction = await db.Database.BeginTransactionAsync();
            var receipt = submissionId.Length == 0 ? null : await db.MockSocialReceipts.FindAsync(submissionId);
            if (receipt is not null)
                return receipt.Operation == operation && receipt.IdNumber == id && receipt.Name == name
                    ? Result(context, true, receipt.Message)
                    : Result(context, false, "业务请求号已用于另一笔申报", 409);
            var employee = await db.MockSocialEmployees.FindAsync(id);
            if (operation == "add" && employee is not null)
                return Result(context, false, "该人员已参保", 409);
            if (operation == "remove" && (employee is null || employee.Name != name))
                return Result(context, false, "参保记录不存在或姓名不匹配", 409);
            if (operation == "add") db.MockSocialEmployees.Add(new MockSocialEmployee { IdNumber = id, Name = name });
            else db.MockSocialEmployees.Remove(employee!);
            var message = operation == "add" ? "增员申报成功" : "减员申报成功";
            if (submissionId.Length > 0)
                db.MockSocialReceipts.Add(new MockSocialReceipt
                {
                    SubmissionId = submissionId, Operation = operation, IdNumber = id, Name = name, Message = message
                });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Result(context, true, message);
        });
    }

    private static bool Authenticated(HttpContext context) =>
        context.Request.Cookies.TryGetValue(SessionCookie, out var token) && token is not null &&
        Sessions.TryGetValue(token, out var expires) && expires > DateTimeOffset.UtcNow;

    private static bool ValidId(string id)
    {
        if (!Regex.IsMatch(id, @"^[1-9]\d{16}[0-9X]$", RegexOptions.CultureInvariant)) return false;
        if (!DateOnly.TryParseExact(id.Substring(6, 8), "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _)) return false;
        var weights = new[] { 7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2 };
        const string checks = "10X98765432";
        var sum = 0;
        for (var i = 0; i < weights.Length; i++) sum += (id[i] - '0') * weights[i];
        return id[17] == checks[sum % 11];
    }

    private static IResult Result(HttpContext context, bool success, string message, int status = 200) =>
        Page(context, success ? "申报完成" : "申报失败",
            $"<p role='status' data-result='{(success ? "success" : "error")}'>{WebUtility.HtmlEncode(message)}</p><a href='{Root}/employees'>返回</a>", status);

    private static IResult Page(HttpContext context, string title, string body, int status = 200) =>
        Results.Content($"<!doctype html><html lang='zh-CN'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'></head><body><main><h1>{WebUtility.HtmlEncode(title)}</h1>{body}</main></body></html>",
            "text/html; charset=utf-8", statusCode: status);
}
