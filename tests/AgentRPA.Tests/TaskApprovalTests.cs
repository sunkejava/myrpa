using AgentRPA.Application.Workflow;
using AgentRPA.Domain.Tasks;

namespace AgentRPA.Tests;

public sealed class TaskApprovalTests
{
    [Fact]
    public void Reviewer_must_be_different_and_decision_is_immutable()
    {
        var requester = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var approval = new TaskApproval(Guid.NewGuid(), requester);
        Assert.Throws<InvalidOperationException>(() => approval.Decide(requester, true, null));
        Assert.Throws<ArgumentException>(() => approval.Decide(reviewer, false, "  "));
        approval.Decide(reviewer, false, "资料不足");
        Assert.Equal(TaskApprovalStatus.Rejected, approval.Status);
        Assert.Equal(reviewer, approval.ReviewerId);
        Assert.Equal("资料不足", approval.Reason);
        Assert.Throws<InvalidOperationException>(() => approval.Decide(Guid.NewGuid(), true, null));
    }

    [Fact]
    public void Published_workflow_risk_and_nested_steps_require_server_approval()
    {
        Assert.True(WorkflowApprovalPolicy.RequiresApproval("""{"riskLevel":"High","steps":[{"type":"End"}]}"""));
        Assert.True(WorkflowApprovalPolicy.RequiresApproval("""{"steps":[{"type":"Condition","config":{"then":[{"type":"Click","requiresApproval":true,"config":{"selector":"#submit"}}]}}]}"""));
        Assert.False(WorkflowApprovalPolicy.RequiresApproval("""{"steps":[{"type":"End"}]}"""));
    }
}
