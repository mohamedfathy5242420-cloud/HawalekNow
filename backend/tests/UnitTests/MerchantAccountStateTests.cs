using HawalekNow.Domain.Modules.Merchants;

namespace HawalekNow.UnitTests;

public sealed class MerchantAccountStateTests
{
    public static IEnumerable<object[]> Decisions()
    {
        foreach (var status in Enum.GetValues<MerchantApprovalStatus>())
        {
            yield return [status, "approve", status == MerchantApprovalStatus.Pending, MerchantApprovalStatus.Approved];
            yield return [status, "changes", status == MerchantApprovalStatus.Pending, MerchantApprovalStatus.ChangesRequested];
            yield return [status, "reject", status == MerchantApprovalStatus.Pending, MerchantApprovalStatus.Rejected];
            yield return [status, "resubmit", status == MerchantApprovalStatus.ChangesRequested, MerchantApprovalStatus.Pending];
            yield return [status, "suspend", status == MerchantApprovalStatus.Approved, MerchantApprovalStatus.Suspended];
            yield return [status, "reinstate", status == MerchantApprovalStatus.Suspended, MerchantApprovalStatus.Approved];
        }
    }

    [Theory, MemberData(nameof(Decisions))]
    public void Only_the_defined_business_transitions_change_state(MerchantApprovalStatus initial, string action, bool allowed, MerchantApprovalStatus target)
    {
        var account = At(initial);
        var changed = action switch
        {
            "approve" => account.TryReview(MerchantApprovalStatus.Approved),
            "changes" => account.TryReview(MerchantApprovalStatus.ChangesRequested),
            "reject" => account.TryReview(MerchantApprovalStatus.Rejected),
            "resubmit" => account.TryResubmit(),
            "suspend" => account.TrySuspend(),
            "reinstate" => account.TryReinstate(),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        Assert.Equal(allowed, changed);
        Assert.Equal(allowed ? target : initial, account.Status);
        Assert.Equal(account.Status is MerchantApprovalStatus.Pending or MerchantApprovalStatus.ChangesRequested or MerchantApprovalStatus.Approved, account.CanPrepareAccount);
        Assert.Equal(account.Status == MerchantApprovalStatus.Approved, account.CanPublish);
    }

    [Theory]
    [InlineData(MerchantApprovalStatus.Pending)]
    [InlineData(MerchantApprovalStatus.Suspended)]
    [InlineData((MerchantApprovalStatus)99)]
    public void A_review_cannot_assign_a_non_review_state(MerchantApprovalStatus invalid)
    {
        var account = At(MerchantApprovalStatus.Pending);
        Assert.Throws<ArgumentOutOfRangeException>(() => account.TryReview(invalid));
        Assert.Equal(MerchantApprovalStatus.Pending, account.Status);
    }

    private static MerchantAccount At(MerchantApprovalStatus status)
    {
        var account = MerchantAccount.CreatePending(Guid.CreateVersion7());
        if (status == MerchantApprovalStatus.Suspended)
        {
            Assert.True(account.TryReview(MerchantApprovalStatus.Approved));
            Assert.True(account.TrySuspend());
        }
        else if (status != MerchantApprovalStatus.Pending) Assert.True(account.TryReview(status));
        return account;
    }
}
