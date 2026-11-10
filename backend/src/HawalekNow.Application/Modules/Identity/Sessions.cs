using System.Text.Json.Serialization;
using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using MediatR;

namespace HawalekNow.Application.Modules.Identity;

// Internal transport to the cookie boundary. It must never become a JSON response or log payload.
public sealed class SessionCredentials(string accessToken, string refreshToken, DateTimeOffset accessExpiresAt, DateTimeOffset refreshExpiresAt)
{
    [JsonIgnore] public string AccessToken { get; } = accessToken;
    [JsonIgnore] public string RefreshToken { get; } = refreshToken;
    [JsonIgnore] public DateTimeOffset AccessExpiresAt { get; } = accessExpiresAt;
    [JsonIgnore] public DateTimeOffset RefreshExpiresAt { get; } = refreshExpiresAt;
    public override string ToString() => "[redacted session credentials]";
}
public sealed record SessionActor(Guid UserId, Guid SessionId, string AccountType, bool EmailConfirmed);
public sealed record LoginCommand(string AccountType, string Identifier, string Password) : IRequest<Result<SessionCredentials>>;
public sealed record RefreshSessionCommand(string? Credential) : IRequest<Result<SessionCredentials>>;
public interface IAccountSessions
{
    Task<Result<SessionCredentials>> LoginAsync(string accountType, string identifier, string password, CancellationToken cancellationToken);
    Task<Result<SessionCredentials>> RefreshAsync(string? credential, CancellationToken cancellationToken);
    Task<SessionActor?> FindActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
}
public sealed class LoginHandler(IAccountSessions service) : IRequestHandler<LoginCommand, Result<SessionCredentials>>
{
    public Task<Result<SessionCredentials>> Handle(LoginCommand request, CancellationToken cancellationToken) => service.LoginAsync(request.AccountType, request.Identifier, request.Password, cancellationToken);
}
public sealed class RefreshSessionHandler(IAccountSessions service) : IRequestHandler<RefreshSessionCommand, Result<SessionCredentials>>
{
    public Task<Result<SessionCredentials>> Handle(RefreshSessionCommand request, CancellationToken cancellationToken) => service.RefreshAsync(request.Credential, cancellationToken);
}
public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(value => value.AccountType).Must(AccountTypeInput.IsValid);
        RuleFor(value => value.Identifier).Must((command, identifier) => EmailRules.IsValid(identifier) ||
            (string.Equals(command.AccountType, "Merchant", StringComparison.OrdinalIgnoreCase) && UsernameRules.IsValid(identifier)));
        RuleFor(value => value.Password).NotEmpty().MaximumLength(512);
    }
}
