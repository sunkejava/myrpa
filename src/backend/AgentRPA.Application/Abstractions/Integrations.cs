namespace AgentRPA.Application.Abstractions;

/// <summary>硬件/UKey统一抽象，屏蔽厂商SDK差异。</summary>
public interface IHardwareCredentialProvider
{
    string ProviderId { get; }
    Task<IReadOnlyList<HardwareDeviceInfo>> DiscoverAsync(CancellationToken cancellationToken);
    Task<HardwareOperationResult> ExecuteAsync(HardwareOperationRequest request, CancellationToken cancellationToken);
}

/// <summary>验证码识别Provider。</summary>
public interface ICaptchaProvider
{
    string ProviderId { get; }
    Task<CaptchaResult> RecognizeAsync(CaptchaRequest request, CancellationToken cancellationToken);
}

/// <summary>统一通知渠道Provider。</summary>
public interface INotificationProvider
{
    string Channel { get; }
    Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}

public sealed record HardwareDeviceInfo(string DeviceId, string ProviderId, string Status, string? CertificateSubject);
public sealed record HardwareOperationRequest(string TaskId, string Operation, IReadOnlyDictionary<string, object?> Parameters);
public sealed record HardwareOperationResult(bool Success, string? ErrorCode = null, string? ErrorMessage = null);
public sealed record CaptchaRequest(string TaskId, string Type, ReadOnlyMemory<byte> Image);
public sealed record CaptchaResult(bool Success, string? Value = null, string? ErrorCode = null);
public sealed record NotificationMessage(string EventCode, string Title, string Body, IReadOnlyList<NotificationAttachment>? Attachments = null);
public sealed record NotificationAttachment(string FileName, string ContentType, Uri? SecureUrl = null, ReadOnlyMemory<byte>? Content = null);
public sealed record NotificationSendResult(bool Success, string? ProviderMessageId = null, string? ErrorCode = null);
