using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.HumanIntervention;

public enum InterventionType { Captcha, QrLogin, FaceAuthentication, UKeyConfirmation, ManualApproval }
public enum InterventionStatus { Pending, Opened, Completed, Expired, Cancelled }

/// <summary>执行过程中的人工介入请求，例如扫码登录、人脸认证、验证码和 UKey 确认。</summary>
public sealed class HumanIntervention : Entity
{
    private HumanIntervention() { }

    public HumanIntervention(Guid executionId, Guid subjectId, InterventionType type, string title, DateTimeOffset expiresAt)
    {
        if (executionId == Guid.Empty) throw new ArgumentException("ExecutionId 不能为空。", nameof(executionId));
        if (subjectId == Guid.Empty) throw new ArgumentException("SubjectId 不能为空。", nameof(subjectId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("标题不能为空。", nameof(title));

        ExecutionId = executionId;
        SubjectId = subjectId;
        Type = type;
        Title = title.Trim();
        ExpiresAt = expiresAt;
    }

    public Guid ExecutionId { get; private set; }
    /// <summary>将人工介入绑定到创建任务的用户，避免跨用户消费介入令牌。</summary>
    public Guid SubjectId { get; private set; }
    public InterventionType Type { get; private set; }
    public InterventionStatus Status { get; private set; } = InterventionStatus.Pending;
    public string Title { get; private set; } = string.Empty;
    /// <summary>仅保存不可逆的令牌摘要，数据库不保存可直接扫码使用的原始密钥。</summary>
    public string? SecureEntryHash { get; private set; }
    public DateTimeOffset? TokenConsumedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public void Open(string? secureEntryHash)
    {
        if (Status != InterventionStatus.Pending) return;
        SecureEntryHash = secureEntryHash;
        Status = InterventionStatus.Opened;
    }

    /// <summary>消费二维码一次性令牌。调用方应先完成用户主体和 Execution 归属校验。</summary>
    public bool TryConsumeQrToken(string tokenHash, DateTimeOffset now)
    {
        if (Type != InterventionType.QrLogin || Status != InterventionStatus.Opened)
            return false;
        if (now > ExpiresAt)
        {
            Status = InterventionStatus.Expired;
            return false;
        }
        if (TokenConsumedAt.HasValue || string.IsNullOrWhiteSpace(SecureEntryHash) ||
            !string.Equals(SecureEntryHash, tokenHash, StringComparison.Ordinal))
            return false;

        TokenConsumedAt = now;
        Status = InterventionStatus.Completed;
        return true;
    }

    public void Complete()
    {
        if (Status != InterventionStatus.Opened) return;
        ExpireIfNeeded(DateTimeOffset.UtcNow);
        if (Status == InterventionStatus.Opened)
            Status = InterventionStatus.Completed;
    }

    public bool ExpireIfNeeded(DateTimeOffset now)
    {
        if (Status is not (InterventionStatus.Pending or InterventionStatus.Opened) || now <= ExpiresAt)
            return false;
        Status = InterventionStatus.Expired;
        return true;
    }

    public void Cancel()
    {
        if (Status is not (InterventionStatus.Pending or InterventionStatus.Opened)) return;
        Status = InterventionStatus.Cancelled;
    }
}
