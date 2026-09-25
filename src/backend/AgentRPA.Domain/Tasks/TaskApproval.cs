using AgentRPA.Domain.Common;

namespace AgentRPA.Domain.Tasks;

/// <summary>高风险任务的独立审批记录。审批人不得是任务发起人。</summary>
public enum TaskApprovalStatus { Pending, Approved, Rejected }

public sealed class TaskApproval : Entity
{
    private TaskApproval() { }
    public TaskApproval(Guid taskId, Guid requesterId)
    {
        if (taskId == Guid.Empty || requesterId == Guid.Empty) throw new ArgumentException("任务及发起人不能为空。");
        TaskId = taskId;
        RequesterId = requesterId;
    }
    public Guid TaskId { get; private set; }
    public Guid RequesterId { get; private set; }
    public Guid? ReviewerId { get; private set; }
    public TaskApprovalStatus Status { get; private set; } = TaskApprovalStatus.Pending;
    public string? Reason { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public void Decide(Guid reviewerId, bool approved, string? reason)
    {
        if (reviewerId == Guid.Empty || reviewerId == RequesterId) throw new InvalidOperationException("审批人必须是另一名管理员。");
        if (Status != TaskApprovalStatus.Pending) throw new InvalidOperationException("该审批已处理，不得重复决定。");
        if (!approved && string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("拒绝时必须填写原因。", nameof(reason));
        if (reason?.Trim().Length > 1000) throw new ArgumentException("审批说明不能超过 1000 个字符。", nameof(reason));
        ReviewerId = reviewerId;
        Status = approved ? TaskApprovalStatus.Approved : TaskApprovalStatus.Rejected;
        Reason = reason?.Trim();
        ReviewedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }
}
