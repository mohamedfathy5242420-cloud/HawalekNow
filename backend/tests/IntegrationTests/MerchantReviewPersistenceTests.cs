using System.Text.Json;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Identity;

namespace HawalekNow.IntegrationTests;

public sealed class MerchantReviewPersistenceTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly IdentityApiFactory factory = new(database);
    public Task InitializeAsync() { _ = factory.Services; return Task.CompletedTask; }
    public Task DisposeAsync() => factory.DisposeAsync().AsTask();
    private static MerchantReviewService Service(HawalekDbContext context) => new(context, new AccountAccess(context, new IdentityStateLock(context)), TimeProvider.System);

    [Fact]
    public async Task Approval_and_audit_are_atomic_and_do_not_copy_credentials()
    {
        var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        var merchant = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        var version = await MerchantTestAccounts.VersionAsync(database, merchant.ProfileId);
        await using (var context = database.CreateContext())
        {
            var result = await Service(context).ReviewAsync(admin.Id, merchant.ProfileId, MerchantReviewAction.Approve, new(version), default);
            Assert.True(result.IsSuccess); Assert.Equal("Approved", result.Value.Status);
        }
        await using var read = database.CreateContext();
        var profile = await read.Set<MerchantAccount>().SingleAsync(item => item.Id == merchant.ProfileId);
        var audit = Assert.Single(await read.Set<AuditEntry>().Where(item => item.TargetId == merchant.ProfileId).ToArrayAsync());
        Assert.Equal(admin.Id, audit.ActorId); Assert.Equal("Merchant.Approve", audit.Operation); Assert.Equal("MerchantAccount", audit.TargetKind);
        Assert.Equal("Pending", audit.PreviousState); Assert.Equal("Approved", audit.CurrentState); Assert.Equal(profile.ReviewedAt, audit.OccurredAt);
        Assert.Equal(admin.Id, profile.ReviewedBy); Assert.Null(audit.Reason); Assert.True(profile.CanPublish);
        Assert.True(await read.Set<ApplicationUser>().Where(item => item.Id == merchant.Id).Select(item => item.EmailConfirmed).SingleAsync());
        var stored = JsonSerializer.Serialize(audit);
        Assert.DoesNotContain(merchant.Password, stored); Assert.DoesNotContain(admin.Password, stored);
        foreach (var hash in await read.Set<ApplicationUser>().Where(item => item.Id == admin.Id || item.Id == merchant.Id).Select(item => item.PasswordHash).ToArrayAsync())
            Assert.DoesNotContain(hash!, stored);
        Assert.DoesNotContain("PasswordHash", stored); Assert.DoesNotContain("RefreshToken", stored); Assert.DoesNotContain("Otp", stored);
        Assert.Empty(read.Model.FindEntityType(typeof(AuditEntry))!.GetForeignKeys());
    }

    [Fact]
    public async Task An_exception_after_both_rows_are_saved_rolls_back_state_metadata_and_audit()
    {
        var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        var merchant = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        var version = await MerchantTestAccounts.VersionAsync(database, merchant.ProfileId);
        var failure = new FailAfterAuditSaved();
        var options = new DbContextOptionsBuilder<HawalekDbContext>().UseSqlServer(database.ConnectionString, sql => sql.UseNetTopologySuite()).AddInterceptors(failure).Options;
        await using (var context = new HawalekDbContext(options))
            await Assert.ThrowsAsync<IOException>(() => Service(context).ReviewAsync(admin.Id, merchant.ProfileId, MerchantReviewAction.Reject, new(version, "Manual rejection."), default));
        Assert.True(failure.SawBothRowsInsideTransaction);
        await using var read = database.CreateContext();
        var after = await read.Set<MerchantAccount>().SingleAsync(item => item.Id == merchant.ProfileId);
        Assert.Equal(MerchantApprovalStatus.Pending, after.Status); Assert.Null(after.ReviewedAt); Assert.Null(after.ReviewReason);
        Assert.Equal(version, Convert.ToBase64String(after.RowVersion));
        Assert.False(await read.Set<AuditEntry>().AnyAsync(item => item.TargetId == merchant.ProfileId));
    }

    [Fact]
    public async Task Opposing_concurrent_reviews_commit_one_decision_and_one_audit()
    {
        var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        var otherAdmin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        var merchant = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        var version = await MerchantTestAccounts.VersionAsync(database, merchant.ProfileId);
        await using var first = database.CreateContext(); await using var second = database.CreateContext();
        var results = await Task.WhenAll(
            Service(first).ReviewAsync(admin.Id, merchant.ProfileId, MerchantReviewAction.Approve, new(version), default),
            Service(second).ReviewAsync(otherAdmin.Id, merchant.ProfileId, MerchantReviewAction.Reject, new(version, "Manual rejection."), default));
        Assert.Single(results, result => result.IsSuccess); Assert.Single(results, result => result.Error?.Kind == ErrorKind.Conflict);
        await using var read = database.CreateContext();
        var audit = Assert.Single(await read.Set<AuditEntry>().Where(item => item.TargetId == merchant.ProfileId).ToArrayAsync());
        var profile = await read.Set<MerchantAccount>().SingleAsync(item => item.Id == merchant.ProfileId);
        Assert.Equal(profile.Status.ToString(), audit.CurrentState);
    }

    [Fact]
    public async Task Application_boundary_rechecks_permissions_email_and_identity_availability()
    {
        var customer = await MerchantTestAccounts.CreateAsync(factory, AccountType.Customer);
        var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        var merchant = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant, confirmed: false);
        var version = await MerchantTestAccounts.VersionAsync(database, merchant.ProfileId);
        await using (var context = database.CreateContext())
            Assert.Equal(ErrorKind.Forbidden, (await Service(context).ReviewAsync(customer.Id, merchant.ProfileId, MerchantReviewAction.Approve, new(version), default)).Error?.Kind);
        await using (var context = database.CreateContext())
            Assert.Equal(ErrorKind.Conflict, (await Service(context).ReviewAsync(admin.Id, merchant.ProfileId, MerchantReviewAction.Approve, new(version), default)).Error?.Kind);
        await using var read = database.CreateContext();
        Assert.Equal(MerchantApprovalStatus.Pending, (await read.Set<MerchantAccount>().SingleAsync(item => item.Id == merchant.ProfileId)).Status);
        Assert.False(await read.Set<AuditEntry>().AnyAsync(item => item.TargetId == merchant.ProfileId));
        await read.Set<ApplicationUser>().Where(item => item.Id == admin.Id).ExecuteUpdateAsync(set => set.SetProperty(item => item.AccessStatus, AccountAccessStatus.Suspended));
        await using var inactive = database.CreateContext();
        Assert.Equal(ErrorKind.Forbidden, (await Service(inactive).ReviewAsync(admin.Id, merchant.ProfileId, MerchantReviewAction.Approve, new(version), default)).Error?.Kind);
    }

    [Fact]
    public async Task Additive_migration_preserves_existing_accounts_and_does_not_fabricate_reviews()
    {
        await using var legacy = new SqlDatabase { InitialMigration = "20261121155717_P03SynonymGroups" };
        await legacy.InitializeAsync();
        var user = ApplicationUser.Create(AccountType.Merchant, "legacy" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N") + "@example.invalid");
        user.NormalizedUserName = user.UserName!.ToUpperInvariant(); user.NormalizedEmail = user.Email!.ToUpperInvariant();
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, "Test-only-" + Guid.NewGuid().ToString("N"));
        var profileId = Guid.CreateVersion7();
        await using (var context = legacy.CreateContext())
        {
            context.Add(user); await context.SaveChangesAsync();
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT [merchants].[Accounts] ([Id],[UserId],[Status]) VALUES ({profileId},{user.Id},4)");
            await context.Database.MigrateAsync();
        }
        await using var read = legacy.CreateContext();
        var after = await read.Set<MerchantAccount>().SingleAsync(item => item.Id == profileId);
        Assert.Equal(MerchantApprovalStatus.Rejected, after.Status); Assert.Null(after.ReviewReason); Assert.Null(after.ReviewedAt); Assert.Null(after.ReviewedBy);
        Assert.Equal(user.PasswordHash, (await read.Set<ApplicationUser>().SingleAsync(item => item.Id == user.Id)).PasswordHash);
        Assert.Empty(await read.Set<AuditEntry>().ToArrayAsync()); Assert.False(read.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Audit_survives_physical_profile_and_identity_removal()
    {
        var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        var merchant = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        await using (var context = database.CreateContext())
        {
            Assert.True((await Service(context).ReviewAsync(admin.Id, merchant.ProfileId, MerchantReviewAction.Reject,
                new(await MerchantTestAccounts.VersionAsync(database, merchant.ProfileId), "Rejected by manual review."), default)).IsSuccess);
            // Fixture-only physical cleanup in its owned SQL database; no deletion API privilege is introduced.
            await context.Set<MerchantAccount>().Where(item => item.Id == merchant.ProfileId).ExecuteDeleteAsync();
            await context.Set<ApplicationUser>().Where(item => item.Id == merchant.Id).ExecuteDeleteAsync();
        }
        await using var read = database.CreateContext();
        Assert.Single(await read.Set<AuditEntry>().Where(item => item.TargetId == merchant.ProfileId).ToArrayAsync());
    }

    private sealed class FailAfterAuditSaved : SaveChangesInterceptor
    {
        public bool SawBothRowsInsideTransaction { get; private set; }
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context!;
            var audit = context.ChangeTracker.Entries<AuditEntry>().Select(entry => entry.Entity).SingleOrDefault();
            if (audit is null) return result;
            SawBothRowsInsideTransaction = context.Database.CurrentTransaction is not null &&
                await context.Set<AuditEntry>().AsNoTracking().AnyAsync(item => item.Id == audit.Id, cancellationToken) &&
                await context.Set<MerchantAccount>().AsNoTracking().AnyAsync(item => item.Id == audit.TargetId && item.Status == MerchantApprovalStatus.Rejected, cancellationToken);
            throw new IOException("Test-only failure after successful SQL SaveChanges.");
        }
    }
}
