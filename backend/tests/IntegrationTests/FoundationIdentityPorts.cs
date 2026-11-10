using HawalekNow.Application.Modules.Identity;
using HawalekNow.Application.Shared.Results;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

// The original isolated pipeline/error hosts validate all handler registrations without running business use cases.
// Real identity HTTP tests boot Program with its actual SQL implementations instead.
public static class FoundationIdentityPorts
{
    public static IServiceCollection AddFoundationIdentityPorts(this IServiceCollection services) =>
        services.AddScoped<IAccountRegistration, UnusedRegistration>();

    private sealed class UnusedRegistration : IAccountRegistration
    {
        public Task<Result> RegisterCustomerAsync(RegistrationDetails details, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<Result> RegisterMerchantAsync(RegistrationDetails details, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An isolated foundation test invoked a business port.");
    }
}
