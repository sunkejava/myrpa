using System.Net.Http.Json;
using AgentRPA.Application.Abstractions;

namespace AgentRPA.Infrastructure.Notifications;

/// <summary>统一 Webhook 通知适配器，可配置到钉钉、企业微信或自建网关。</summary>
public sealed class WebhookNotificationProvider(HttpClient client, string channel, Uri endpoint) : INotificationProvider
{
    public string Channel => channel;

    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                msgtype = "text",
                text = new { content = $"{message.Title}\n{message.Body}" },
                eventCode = message.EventCode
            };
            using var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(false, ErrorCode: $"HTTP_{(int)response.StatusCode}");
            return new(true);
        }
        catch (Exception ex)
        {
            return new(false, ErrorCode: "WEBHOOK_ERROR", ErrorMessage: ex.Message);
        }
    }
}

/// <summary>SMTP 邮件通知适配器，密码由部署环境配置注入，禁止写入代码和日志。</summary>
public sealed class SmtpNotificationProvider(string host, int port, string user, string password, string from, string recipient) : INotificationProvider
{
    public string Channel => "Email";

    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using var smtp = new System.Net.Mail.SmtpClient(host, port) { EnableSsl = true, Credentials = new System.Net.NetworkCredential(user, password) };
            using var mail = new System.Net.Mail.MailMessage(from, recipient, message.Title, message.Body);
            await smtp.SendMailAsync(mail, cancellationToken);
            return new(true);
        }
        catch (Exception ex)
        {
            return new(false, ErrorCode: "SMTP_ERROR", ErrorMessage: ex.Message);
        }
    }
}
