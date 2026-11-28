using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

// Technical pipeline/error/logger hosts validate all handlers without SQL or business endpoints.
// Merchant business tests boot Program with the real SQL implementation instead.
public static class FoundationMerchantPorts
{
    public static IServiceCollection AddFoundationMerchantPorts(this IServiceCollection services) =>
        services.AddScoped<IMerchantReviewService, UnusedReview>().AddScoped<IStoreService, UnusedStore>().AddScoped<IBranchService, UnusedBranches>().AddScoped<IActivityChangeService, UnusedActivityChanges>().AddScoped<IStoreAdministration, UnusedAdministration>();

    private sealed class UnusedAdministration : IStoreAdministration
    {
        public Task<Result<AdminStoreDto>> ReadAsync(Guid actorId, Guid id, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<MerchantPage<AdminStoreDto>>> ListAsync(Guid actorId, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<StoreDto>> UpdateAsync(Guid actorId, Guid id, AdminStoreUpdateWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<StoreDto>> ModerateAsync(Guid actorId, Guid id, StoreAdministrationAction action, StoreModerationWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<StoreDto>> ChangeLogoAsync(Guid actorId, Guid id, string rowVersion, Stream? content, string reason, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<byte[]>> ReadLogoAsync(Guid actorId, Guid id, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<MerchantPage<BranchDto>>> BranchesAsync(Guid actorId, Guid storeId, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<BranchDto>> UpdateBranchAsync(Guid actorId, Guid storeId, Guid id, AdminBranchUpdateWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<MerchantPage<MerchantAuditDto>>> AuditAsync(Guid actorId, Guid id, bool branch, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
    }

    private sealed class UnusedActivityChanges : IActivityChangeService
    {
        public Task<Result<ActivityChangeDto>> RequestAsync(Guid actorId, Guid storeId, ActivityChangeWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<ActivityChangeDto>> ReviewAsync(Guid actorId, Guid id, bool approve, ActivityChangeReviewWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<MerchantPage<ActivityChangeDto>>> OwnListAsync(Guid actorId, Guid storeId, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<MerchantPage<ActivityChangeDto>>> AdminListAsync(Guid actorId, string? status, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
    }

    private sealed class UnusedBranches : IBranchService
    {
        public Task<Result<BranchDto>> CreateAsync(Guid actorId, Guid storeId, BranchWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<BranchDto>> UpdateAsync(Guid actorId, Guid storeId, Guid id, BranchUpdateWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<MerchantPage<BranchDto>>> OwnListAsync(Guid actorId, Guid storeId, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<MerchantPage<PublicBranchDto>>> PublicListAsync(Guid storeId, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<PublicBranchDto>> PublicAsync(Guid id, CancellationToken cancellationToken) => throw Unused();
    }

    private sealed class UnusedStore : IStoreService
    {
        public Task<Result<StoreDto>> CreateAsync(Guid actorId, StoreCreateWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<StoreDto>> UpdateAsync(Guid actorId, Guid id, StoreUpdateWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<StoreDto>> OwnAsync(Guid actorId, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<PublicStoreDto>> PublicAsync(Guid id, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<StoreDto>> ChangeLogoAsync(Guid actorId, Guid id, string rowVersion, Stream? content, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<byte[]>> ReadLogoAsync(Guid? actorId, Guid id, CancellationToken cancellationToken) => throw Unused();
    }

    private sealed class UnusedReview : IMerchantReviewService
    {
        public Task<Result<MerchantReviewDto>> ReviewAsync(Guid actorId, Guid id, MerchantReviewAction action, MerchantReviewWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<MerchantReviewDto>> ResubmitAsync(Guid actorId, string rowVersion, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<MerchantReviewDto>> OwnAsync(Guid actorId, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<MerchantPage<MerchantReviewDto>>> ListAsync(Guid actorId, string? status, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<MerchantPage<MerchantAuditDto>>> AuditAsync(Guid actorId, Guid id, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
    }

    private static InvalidOperationException Unused() => new("An isolated foundation test invoked a business port.");
}
