using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;

namespace HawalekNow.UnitTests;

public sealed class MerchantReviewTests
{
    [Theory]
    [InlineData(MerchantReviewAction.Approve, null, true)]
    [InlineData(MerchantReviewAction.RequestChanges, null, false)]
    [InlineData(MerchantReviewAction.Reject, "  ", false)]
    [InlineData(MerchantReviewAction.Suspend, null, false)]
    [InlineData(MerchantReviewAction.Reinstate, null, false)]
    [InlineData(MerchantReviewAction.RequestChanges, "Provide a public contact number.", true)]
    [InlineData(MerchantReviewAction.Reject, "Duplicate account.", true)]
    [InlineData(MerchantReviewAction.Suspend, "Manual moderation.", true)]
    [InlineData(MerchantReviewAction.Reinstate, "Issue resolved.", true)]
    public void Consequential_decisions_require_an_appropriate_reason(MerchantReviewAction action, string? reason, bool accepted) =>
        Assert.Equal(accepted, MerchantReviewInput.Write(new("AAAAAAAAAAA=", reason), action));

    [Fact]
    public void Input_is_bounded_and_versions_must_be_SQL_rowversions()
    {
        Assert.False(MerchantReviewInput.Write(new("AAAAAAAAAAA=", new string('x', 1001)), MerchantReviewAction.Approve));
        Assert.False(MerchantReviewInput.Write(new("AAAAAAAAAAA=", "bad\0reason"), MerchantReviewAction.Reject));
        Assert.False(MerchantReviewInput.Write(new("bad-base64", "Reason"), MerchantReviewAction.Reject));
        Assert.False(MerchantReviewInput.Version(Convert.ToBase64String(new byte[7])));
        Assert.False(MerchantReviewInput.Status("3"));
        Assert.False(MerchantReviewInput.Page(0, 25));
        Assert.False(MerchantReviewInput.Page(1, 101));
    }

    [Fact]
    public void Review_metadata_keeps_the_reason_and_absolute_time_without_changing_Identity()
    {
        var account = MerchantAccount.CreatePending(Guid.CreateVersion7());
        var actor = Guid.CreateVersion7(); var instant = new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.FromHours(2));
        Assert.True(account.TryReview(MerchantApprovalStatus.ChangesRequested));
        Assert.Throws<ArgumentException>(() => account.RecordReview(actor, " ", instant));
        account.RecordReview(actor, "  Correct the contact number.  ", instant);
        Assert.Equal("Correct the contact number.", account.ReviewReason);
        Assert.Equal(actor, account.ReviewedBy);
        Assert.Equal(instant.ToUniversalTime(), account.ReviewedAt);
        Assert.True(account.TryResubmit());
        Assert.Equal("Correct the contact number.", account.ReviewReason);
        Assert.Throws<InvalidOperationException>(() => account.RecordReview(actor, "Not a decision", instant));
    }

    [Fact]
    public void Audit_has_explicit_bounded_metadata_and_UTC_time()
    {
        var actor = Guid.CreateVersion7(); var target = Guid.CreateVersion7();
        var time = new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.FromHours(2));
        var entry = AuditEntry.Record(actor, "Merchant.Approve", "MerchantAccount", target, null, time, "Pending", "Approved");
        Assert.Equal(7, entry.Id.Version); Assert.Equal(actor, entry.ActorId); Assert.Equal(target, entry.TargetId);
        Assert.Equal(TimeSpan.Zero, entry.OccurredAt.Offset); Assert.Equal(time.ToUniversalTime(), entry.OccurredAt);
        Assert.Null(entry.Reason);
        Assert.Throws<ArgumentException>(() => AuditEntry.Record(Guid.Empty, "Merchant.Approve", "MerchantAccount", target, null, time));
        Assert.Throws<ArgumentException>(() => AuditEntry.Record(actor, "Invalid operation payload", "MerchantAccount", target, null, time));
        Assert.Throws<ArgumentException>(() => AuditEntry.Record(actor, "Merchant.Approve", "MerchantAccount", target, new string('x', 1001), time));
    }
}
