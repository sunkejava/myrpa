using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace AgentRPA.Api.Mock;

// This isolated fixture is deliberately registered only in Development. It has no production routes.
public static class MockSocialSecurity
{
    private const string Root = "/mock/qd-social-security";
    private const string SessionCookie = "qd-social-session";
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Sessions = new();
    private static readonly ConcurrentDictionary<string, string> Employees = new();

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
                  <button type="submit">提交申报</button>
                </form>
                """);
        });

        app.MapPost(Root + "/employees", async (HttpContext context) =>
        {
            if (!Authenticated(context)) return Results.Redirect(Root);
            var form = await context.Request.ReadFormAsync();
            var name = form["employeeName"].ToString().Trim();
            var id = form["idNumber"].ToString().Trim().ToUpperInvariant();
            var operation = form["operation"].ToString();
            if (name.Length is < 2 or > 40 || !ValidId(id) || operation is not ("add" or "remove"))
                return Result(context, false, "姓名、身份证号或业务类型无效", 400);

            var success = operation == "add" ? Employees.TryAdd(id, name)
                : ((ICollection<KeyValuePair<string, string>>)Employees).Remove(new KeyValuePair<string, string>(id, name));
            if (!success) return Result(context, false, operation == "add" ? "该人员已参保" : "参保记录不存在或姓名不匹配", 409);
            return Result(context, true, operation == "add" ? "增员申报成功" : "减员申报成功");
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
