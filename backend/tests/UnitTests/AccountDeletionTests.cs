using HawalekNow.Domain.Modules.Identity;

namespace HawalekNow.UnitTests;

public sealed class AccountDeletionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 0, 0, 0, TimeSpan.Zero);
    [Theory]
    [InlineData(AccountType.Customer)]
    [InlineData(AccountType.Merchant)]
    public void Deadline_is_exact_and_duplicate_request_cannot_extend_it(AccountType type)
    {
        var deletion = AccountDeletion.Create(Guid.CreateVersion7(), type, Now); var generation = deletion.Generation;
        Assert.Equal(Now.AddDays(30), deletion.PurgeAt); Assert.True(deletion.CanRecover(Now.AddDays(30).AddTicks(-1)));
        Assert.False(deletion.Request(type, Now.AddDays(1))); Assert.Equal(generation, deletion.Generation); Assert.Equal(Now.AddDays(30), deletion.PurgeAt);
        Assert.False(deletion.CanRecover(Now.AddDays(30))); Assert.False(deletion.Restore(Now.AddDays(30)));
        Assert.True(deletion.CanPurge(generation, Now.AddDays(30))); Assert.False(deletion.CanPurge(Guid.CreateVersion7(), Now.AddDays(31)));
    }
    [Fact]
    public void Recovery_and_new_generation_make_old_jobs_ineligible()
    {
        var deletion = AccountDeletion.Create(Guid.CreateVersion7(), AccountType.Customer, Now); var previous = deletion.Generation;
        Assert.True(deletion.Restore(Now.AddDays(1))); Assert.False(deletion.CanPurge(previous, Now.AddDays(31)));
        Assert.True(deletion.Request(AccountType.Customer, Now.AddDays(2))); Assert.NotEqual(previous, deletion.Generation);
        Assert.False(deletion.CanPurge(previous, Now.AddDays(40))); Assert.True(deletion.CanPurge(deletion.Generation, Now.AddDays(32)));
    }
    [Fact]
    public void Admin_and_invalid_owners_cannot_request_deletion()
    {
        Assert.Throws<InvalidOperationException>(() => AccountDeletion.Create(Guid.CreateVersion7(), AccountType.Admin, Now));
        Assert.Throws<ArgumentException>(() => AccountDeletion.Create(Guid.Empty, AccountType.Customer, Now));
    }
}
