using HawalekNow.Domain.Modules.Merchants;

namespace HawalekNow.UnitTests;

public sealed class MerchantAccountTests
{
    [Fact]
    public void A_new_merchant_is_pending_and_can_prepare_but_is_not_approved()
    {
        var owner = Guid.CreateVersion7();
        var account = MerchantAccount.CreatePending(owner);
        Assert.Equal(owner, account.UserId);
        Assert.Equal(7, account.Id.Version);
        Assert.Equal(MerchantApprovalStatus.Pending, account.Status);
        Assert.True(account.CanPrepareAccount);
        Assert.Empty(account.RowVersion);
    }

    [Fact]
    public void An_empty_owner_cannot_create_a_merchant_account() =>
        Assert.Throws<ArgumentException>(() => MerchantAccount.CreatePending(Guid.Empty));
}
