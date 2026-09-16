using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.HumanIntervention;

public enum InterventionType { Captcha, QrLogin, FaceAuthentication, UKeyConfirmation, ManualApproval }
public enum InterventionStatus { Pending, Opened, Completed, Expired, Cancelled }

/// <summary>执行过程中的人工介入请求，例如扫码登录、人脸认证、验证码和 UKey 确认。</summary>
public sealed class HumanIntervention : Entity
{
    private HumanIntervention() { }

    public HumanIntervention(Guid executionId, InterventionType type, string title, DateTimeOffset expiresAt)
    {
        ExecutionId = executionId;
        Type = type;
        Title = title;
        ExpiresAt = expiresAt;
    }

    public Guid ExecutionId { get; private set; }
    public InterventionType Type { get; private set; }
    public InterventionStatus Status { get; private set; } = InterventionStatus.Pending;
    public string Title { get; private set; } = string.Empty;
    public string? SecureEntry { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public void Open(string? secureEntry)
    {
        if (Status != InterventionStatus.Pending) return;
        SecureEntry = secureEntry;
        Status = InterventionStatus.Opened;
    }

    public void Complete()
    {
        if (Status != InterventionStatus.Opened) return;
        Status = DateTimeOffset.UtcNow <= ExpiresAt
            ? InterventionStatus.Completed
            : InterventionStatus.Expired;
    }

    public void Cancel()
    {
        if (Status is not (InterventionStatus.Pending or InterventionStatus.Opened)) return;
        Status = InterventionStatus.Cancelled;
    }
}
