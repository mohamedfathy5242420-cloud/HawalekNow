using HawalekNow.Domain.Modules.Merchants;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.IntegrationTests;

public sealed class MerchantAccountStateTests(SqlDatabase database) : IClassFixture<SqlDatabase>
{
    [Fact]
    public async Task Full_review_resubmission_and_suspension_lifecycle_persists_without_a_schema_change()
    {
        var account = MerchantAccount.CreatePending(Guid.CreateVersion7());
        await using var context = database.CreateContext();
        context.Add(account);
        await context.SaveChangesAsync();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.True(account.TryReview(MerchantApprovalStatus.ChangesRequested));
        await context.SaveChangesAsync();
        Assert.Equal(MerchantApprovalStatus.ChangesRequested, await ReadStatusAsync(account.Id));
        Assert.True(account.TryResubmit());
        await context.SaveChangesAsync();
        Assert.Equal(MerchantApprovalStatus.Pending, await ReadStatusAsync(account.Id));
        Assert.True(account.TryReview(MerchantApprovalStatus.Approved));
        await context.SaveChangesAsync();
        Assert.Equal(MerchantApprovalStatus.Approved, await ReadStatusAsync(account.Id));
        Assert.True(account.TrySuspend());
        await context.SaveChangesAsync();
        Assert.Equal(MerchantApprovalStatus.Suspended, await ReadStatusAsync(account.Id));
        Assert.True(account.TryReinstate());
        await context.SaveChangesAsync();
        Assert.Equal(MerchantApprovalStatus.Approved, await ReadStatusAsync(account.Id));

        var rejected = MerchantAccount.CreatePending(Guid.CreateVersion7());
        Assert.True(rejected.TryReview(MerchantApprovalStatus.Rejected));
        context.Add(rejected);
        await context.SaveChangesAsync();
        Assert.Equal(MerchantApprovalStatus.Rejected, await ReadStatusAsync(rejected.Id));
        Assert.False(rejected.TryResubmit());
    }

    [Fact]
    public async Task Opposing_review_decisions_cannot_overwrite_a_committed_decision()
    {
        await using var first = database.CreateContext();
        var account = MerchantAccount.CreatePending(Guid.CreateVersion7());
        first.Add(account);
        await first.SaveChangesAsync();
        await using var second = database.CreateContext();
        var stale = await second.Set<MerchantAccount>().SingleAsync(item => item.Id == account.Id);
        Assert.True(account.TryReview(MerchantApprovalStatus.Approved));
        await first.SaveChangesAsync();
        Assert.True(stale.TryReview(MerchantApprovalStatus.Rejected));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        Assert.Equal(MerchantApprovalStatus.Approved, await ReadStatusAsync(account.Id));
    }

    [Fact]
    public async Task A_saved_but_uncommitted_decision_rolls_back()
    {
        var account = MerchantAccount.CreatePending(Guid.CreateVersion7());
        await using (var context = database.CreateContext())
        {
            context.Add(account);
            await context.SaveChangesAsync();
            await using var transaction = await context.BeginAsync();
            Assert.True(account.TryReview(MerchantApprovalStatus.Rejected));
            await context.SaveChangesAsync();
        }
        Assert.Equal(MerchantApprovalStatus.Pending, await ReadStatusAsync(account.Id));
    }

    private async Task<MerchantApprovalStatus> ReadStatusAsync(Guid id)
    {
        await using var read = database.CreateContext();
        return await read.Set<MerchantAccount>().Where(item => item.Id == id).Select(item => item.Status).SingleAsync();
    }
}
