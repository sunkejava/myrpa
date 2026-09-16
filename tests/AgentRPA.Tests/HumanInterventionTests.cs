using AgentRPA.Domain.HumanIntervention;

namespace AgentRPA.Tests;

public sealed class HumanInterventionTests
{
    [Fact]
    public void Pending_intervention_can_be_opened_and_completed_before_expiry()
    {
        var intervention = new HumanIntervention(
            Guid.NewGuid(), InterventionType.QrLogin, "扫码登录", DateTimeOffset.UtcNow.AddMinutes(5));

        intervention.Open("short-lived-token");
        intervention.Complete();

        Assert.Equal(InterventionStatus.Completed, intervention.Status);
        Assert.Equal("short-lived-token", intervention.SecureEntry);
    }

    [Fact]
    public void Expired_intervention_cannot_be_completed_successfully()
    {
        var intervention = new HumanIntervention(
            Guid.NewGuid(), InterventionType.ManualApproval, "人工确认", DateTimeOffset.UtcNow.AddSeconds(-1));

        intervention.Open(null);
        intervention.Complete();

        Assert.Equal(InterventionStatus.Expired, intervention.Status);
    }

    [Fact]
    public void Open_does_not_reopen_or_replace_a_non_pending_intervention()
    {
        var intervention = new HumanIntervention(
            Guid.NewGuid(), InterventionType.UKeyConfirmation, "UKey确认", DateTimeOffset.UtcNow.AddMinutes(5));

        intervention.Open("first");
        intervention.Open("second");

        Assert.Equal(InterventionStatus.Opened, intervention.Status);
        Assert.Equal("first", intervention.SecureEntry);
    }

    [Fact]
    public void Cancelled_intervention_cannot_be_reopened()
    {
        var intervention = new HumanIntervention(
            Guid.NewGuid(), InterventionType.FaceAuthentication, "人工接管认证", DateTimeOffset.UtcNow.AddMinutes(5));

        intervention.Cancel();
        intervention.Open("entry");

        Assert.Equal(InterventionStatus.Cancelled, intervention.Status);
        Assert.Null(intervention.SecureEntry);
    }
}
