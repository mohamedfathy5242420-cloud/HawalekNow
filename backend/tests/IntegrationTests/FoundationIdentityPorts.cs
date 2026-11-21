using HawalekNow.Application.Modules.Identity;
using HawalekNow.Application.Shared.Results;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

// The original isolated pipeline/error hosts validate all handler registrations without running business use cases.
// Real identity HTTP tests boot Program with its actual SQL implementations instead.
public static class FoundationIdentityPorts
{
    public static IServiceCollection AddFoundationIdentityPorts(this IServiceCollection services) =>
        services.AddScoped<IAccountRegistration, UnusedRegistration>().AddScoped<IEmailConfirmation, UnusedEmail>().AddScoped<IAccountSessions, UnusedSessions>()
            .AddScoped<IIdentityAccountQueries, UnusedQueries>().AddScoped<IPasswordRecovery, UnusedPasswordRecovery>().AddScoped<IAccountSessionManagement, UnusedSessionManagement>().AddScoped<IAccountLifecycle, UnusedLifecycle>();

    private sealed class UnusedLifecycle : IAccountLifecycle
    {
        public Task<Result> DeleteAsync(Guid userId, string password, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<Result> RequestRecoveryAsync(string accountType, string email, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<Result> RecoverAsync(string accountType, string email, string code, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
    }

    private sealed class UnusedPasswordRecovery : IPasswordRecovery
    {
        public Task<Result> RequestAsync(string accountType, string email, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<Result> ResetAsync(string accountType, string email, string code, string newPassword, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
    }
    private sealed class UnusedSessionManagement : IAccountSessionManagement
    {
        public Task<Result> LogoutAsync(string? refreshCredential, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<Result> LogoutAllAsync(Guid userId, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<Result> RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
    }

    private sealed class UnusedQueries : IIdentityAccountQueries
    {
        public Task<CustomerIdentityInfo?> CustomerIdentityAsync(Guid userId, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<IReadOnlyList<AccountSessionInfo>> SessionsAsync(Guid userId, Guid currentSessionId, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<AccountSessionInfo?> SessionAsync(Guid userId, Guid sessionId, Guid currentSessionId, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
    }

    private sealed class UnusedSessions : IAccountSessions
    {
        public Task<Result<SessionCredentials>> LoginAsync(string accountType, string identifier, string password, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<Result<SessionCredentials>> RefreshAsync(string? credential, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<SessionActor?> FindActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
    }

    private sealed class UnusedEmail : IEmailConfirmation
    {
        public Task<Result> RequestAsync(string accountType, string email, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<Result> VerifyAsync(string accountType, string email, string code, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
    }

    private sealed class UnusedRegistration : IAccountRegistration
    {
        public Task<Result> RegisterCustomerAsync(RegistrationDetails details, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<Result> RegisterMerchantAsync(RegistrationDetails details, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An isolated foundation test invoked a business port.");
    }
}
