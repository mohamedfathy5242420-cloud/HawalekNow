using HawalekNow.Application.Modules.Merchants.Contracts;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Persistence;
using HawalekNow.Infrastructure.Shared.Storage;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Application.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed class MerchantAccounts(HawalekDbContext context, Func<LogoDeletionIntents>? deletionIntents = null) : IMerchantAccounts
{
    public Task CreatePendingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        context.Set<MerchantAccount>().Add(MerchantAccount.CreatePending(userId));
        return Task.CompletedTask;
    }

    public async Task<MerchantAccountInfo?> FindByOwnerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await context.Set<MerchantAccount>().AsNoTracking().SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);
        return account is null ? null : new(account.Id, account.Status.ToString(), account.CanPrepareAccount,
            account.Status == MerchantApprovalStatus.Approved, account.Status == MerchantApprovalStatus.Suspended);
    }
    public async Task RemoveIdentityProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Profile removal requires the owning SQL transaction.");
        var profileIds = context.Set<MerchantAccount>().Where(value => value.UserId == userId).Select(value => value.Id);
        var keys = await context.Set<Store>().Where(value => profileIds.Contains(value.MerchantAccountId) && value.LogoKey != null).Select(value => value.LogoKey!).ToArrayAsync(cancellationToken);
        if (keys.Length > 0)
        {
            // Defer resolving Outbox until commercial data actually exists. Profile-only callers need no file services.
            var intents = deletionIntents?.Invoke() ?? throw new InvalidOperationException("Commercial profile removal requires transactional file deletion intents.");
            foreach (var key in keys) intents.Add(key, userId);
        }
        // Owned stores cascade in SQL. File removal is retried only after the owning purge transaction commits.
        await context.Set<MerchantAccount>().Where(value => value.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }
}

public static class MerchantRegistration
{
    public static IServiceCollection AddMerchantAccounts(this IServiceCollection services) => services.AddOutboxPersistence()
        .AddScoped<IMerchantAccounts>(provider => new MerchantAccounts(provider.GetRequiredService<HawalekDbContext>(), () => provider.GetRequiredService<LogoDeletionIntents>()))
        .AddScoped<LogoDeletionIntents>()
        .AddScoped<IMerchantReviewService, MerchantReviewService>();

    public static IServiceCollection AddMerchantStores(this IServiceCollection services) => services
        .AddLocalFileStorage().AddScoped<StoredFileLock>().AddScoped<MerchantStoreAccess>()
        .AddScoped<LogoCleanup>().AddScoped<LogoReconciliationJob>()
        .AddScoped<IOutboxHandler, LogoDeleteHandler>()
        .AddScoped<IStoreService, StoreService>().AddHostedService<MerchantMaintenanceScheduler>();
}
