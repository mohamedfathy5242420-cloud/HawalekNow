using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

// Technical pipeline/error/logger hosts validate all handlers without SQL or business endpoints.
// Merchant business tests boot Program with the real SQL implementation instead.
public static class FoundationMerchantPorts
{
    public static IServiceCollection AddFoundationMerchantPorts(this IServiceCollection services) =>
        services.AddScoped<IMerchantReviewService, UnusedReview>();

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
