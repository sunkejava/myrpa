using System.Security.Cryptography;
using System.Text;
using AgentRPA.Domain.HumanIntervention;

namespace AgentRPA.Tests;

public sealed class HumanInterventionTests
{
    [Fact]
    public void Pending_intervention_can_be_opened_and_completed_before_expiry()
    {
        var subjectId = Guid.NewGuid();
        var intervention = new HumanIntervention(
            Guid.NewGuid(), subjectId, InterventionType.QrLogin, "扫码登录", DateTimeOffset.UtcNow.AddMinutes(5));

        intervention.Open(Hash("short-lived-token"));
        var consumed = intervention.TryConsumeQrToken(Hash("short-lived-token"), DateTimeOffset.UtcNow);

        Assert.True(consumed);
        Assert.Equal(subjectId, intervention.SubjectId);
        Assert.Equal(InterventionStatus.Completed, intervention.Status);
        Assert.NotNull(intervention.TokenConsumedAt);
    }

    [Fact]
    public void Qr_token_can_only_be_consumed_once()
    {
        var intervention = new HumanIntervention(
            Guid.NewGuid(), Guid.NewGuid(), InterventionType.QrLogin, "扫码登录", DateTimeOffset.UtcNow.AddMinutes(5));
        intervention.Open(Hash("one-time-token"));

        Assert.True(intervention.TryConsumeQrToken(Hash("one-time-token"), DateTimeOffset.UtcNow));
        Assert.False(intervention.TryConsumeQrToken(Hash("one-time-token"), DateTimeOffset.UtcNow));
        Assert.Equal(InterventionStatus.Completed, intervention.Status);
    }

    [Fact]
    public void Qr_token_expires_and_cannot_be_consumed()
    {
        var intervention = new HumanIntervention(
            Guid.NewGuid(), Guid.NewGuid(), InterventionType.QrLogin, "扫码登录", DateTimeOffset.UtcNow.AddSeconds(-1));
        intervention.Open(Hash("expired-token"));

        Assert.False(intervention.TryConsumeQrToken(Hash("expired-token"), DateTimeOffset.UtcNow));
        Assert.Equal(InterventionStatus.Expired, intervention.Status);
    }

    [Fact]
    public void Wrong_qr_token_does_not_complete_intervention()
    {
        var intervention = new HumanIntervention(
            Guid.NewGuid(), Guid.NewGuid(), InterventionType.QrLogin, "扫码登录", DateTimeOffset.UtcNow.AddMinutes(5));
        intervention.Open(Hash("expected-token"));

        Assert.False(intervention.TryConsumeQrToken(Hash("wrong-token"), DateTimeOffset.UtcNow));
        Assert.Equal(InterventionStatus.Opened, intervention.Status);
        Assert.Null(intervention.TokenConsumedAt);
    }

    [Fact]
    public void Expired_intervention_cannot_be_completed_successfully()
    {
        var intervention = new HumanIntervention(
            Guid.NewGuid(), Guid.NewGuid(), InterventionType.ManualApproval, "人工确认", DateTimeOffset.UtcNow.AddSeconds(-1));

        intervention.Open(null);
        intervention.Complete();

        Assert.Equal(InterventionStatus.Expired, intervention.Status);
    }

    [Fact]
    public void Open_does_not_reopen_or_replace_a_non_pending_intervention()
    {
        var intervention = new HumanIntervention(
            Guid.NewGuid(), Guid.NewGuid(), InterventionType.UKeyConfirmation, "UKey确认", DateTimeOffset.UtcNow.AddMinutes(5));

        intervention.Open("first");
        intervention.Open("second");

        Assert.Equal(InterventionStatus.Opened, intervention.Status);
        Assert.Equal("first", intervention.SecureEntryHash);
    }

    [Fact]
    public void Cancelled_intervention_cannot_be_reopened()
    {
        var intervention = new HumanIntervention(
            Guid.NewGuid(), Guid.NewGuid(), InterventionType.FaceAuthentication, "人工接管认证", DateTimeOffset.UtcNow.AddMinutes(5));

        intervention.Cancel();
        intervention.Open("entry");

        Assert.Equal(InterventionStatus.Cancelled, intervention.Status);
        Assert.Null(intervention.SecureEntryHash);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
