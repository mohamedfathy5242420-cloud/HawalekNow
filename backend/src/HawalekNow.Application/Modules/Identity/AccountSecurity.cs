using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using MediatR;

namespace HawalekNow.Application.Modules.Identity;

public interface IPasswordRecovery
{
    Task<Result> RequestAsync(string accountType, string email, CancellationToken cancellationToken);
    Task<Result> ResetAsync(string accountType, string email, string code, string newPassword, CancellationToken cancellationToken);
}
public interface IAccountSessionManagement
{
    Task<Result> LogoutAsync(string? refreshCredential, CancellationToken cancellationToken);
    Task<Result> LogoutAllAsync(Guid userId, CancellationToken cancellationToken);
    Task<Result> RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
}
public sealed record RequestPasswordResetCommand(string AccountType, string Email) : IRequest<Result>;
public sealed record ResetPasswordCommand(string AccountType, string Email, string Code, string NewPassword) : IRequest<Result>;
public sealed record LogoutCommand(string? RefreshCredential) : IRequest<Result>;
public sealed record LogoutAllCommand(Guid UserId) : IRequest<Result>;
public sealed record RevokeSessionCommand(Guid UserId, Guid SessionId) : IRequest<Result>;
public sealed class RequestPasswordResetHandler(IPasswordRecovery service) : IRequestHandler<RequestPasswordResetCommand, Result>
{
    public Task<Result> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken) => service.RequestAsync(request.AccountType, request.Email, cancellationToken);
}
public sealed class ResetPasswordHandler(IPasswordRecovery service) : IRequestHandler<ResetPasswordCommand, Result>
{
    public Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken) => service.ResetAsync(request.AccountType, request.Email, request.Code, request.NewPassword, cancellationToken);
}
public sealed class LogoutHandler(IAccountSessionManagement service) : IRequestHandler<LogoutCommand, Result>
{
    public Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken) => service.LogoutAsync(request.RefreshCredential, cancellationToken);
}
public sealed class LogoutAllHandler(IAccountSessionManagement service) : IRequestHandler<LogoutAllCommand, Result>
{
    public Task<Result> Handle(LogoutAllCommand request, CancellationToken cancellationToken) => service.LogoutAllAsync(request.UserId, cancellationToken);
}
public sealed class RevokeSessionHandler(IAccountSessionManagement service) : IRequestHandler<RevokeSessionCommand, Result>
{
    public Task<Result> Handle(RevokeSessionCommand request, CancellationToken cancellationToken) => service.RevokeAsync(request.UserId, request.SessionId, cancellationToken);
}
public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator() { RuleFor(value => value.AccountType).Must(AccountTypeInput.IsValid); RuleFor(value => value.Email).Must(EmailRules.IsValid); }
}
public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(value => value.AccountType).Must(AccountTypeInput.IsValid); RuleFor(value => value.Email).Must(EmailRules.IsValid);
        RuleFor(value => value.Code).NotNull().Matches("^[0-9]{6}$"); RuleFor(value => value.NewPassword).Must(PasswordRules.IsAcceptable);
    }
}
