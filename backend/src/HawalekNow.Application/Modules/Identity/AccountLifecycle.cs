using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using MediatR;

namespace HawalekNow.Application.Modules.Identity;

public interface IAccountLifecycle
{
    Task<Result> DeleteAsync(Guid userId, string password, CancellationToken cancellationToken);
    Task<Result> RequestRecoveryAsync(string accountType, string email, CancellationToken cancellationToken);
    Task<Result> RecoverAsync(string accountType, string email, string code, CancellationToken cancellationToken);
}
public sealed record DeleteAccountCommand(Guid UserId, string Password) : IRequest<Result>;
public sealed record RequestAccountRecoveryCommand(string AccountType, string Email) : IRequest<Result>;
public sealed record RecoverAccountCommand(string AccountType, string Email, string Code) : IRequest<Result>;
public sealed class DeleteAccountHandler(IAccountLifecycle service) : IRequestHandler<DeleteAccountCommand, Result>
{
    public Task<Result> Handle(DeleteAccountCommand request, CancellationToken cancellationToken) => service.DeleteAsync(request.UserId, request.Password, cancellationToken);
}
public sealed class RequestAccountRecoveryHandler(IAccountLifecycle service) : IRequestHandler<RequestAccountRecoveryCommand, Result>
{
    public Task<Result> Handle(RequestAccountRecoveryCommand request, CancellationToken cancellationToken) => service.RequestRecoveryAsync(request.AccountType, request.Email, cancellationToken);
}
public sealed class RecoverAccountHandler(IAccountLifecycle service) : IRequestHandler<RecoverAccountCommand, Result>
{
    public Task<Result> Handle(RecoverAccountCommand request, CancellationToken cancellationToken) => service.RecoverAsync(request.AccountType, request.Email, request.Code, cancellationToken);
}
public sealed class DeleteAccountValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountValidator() { RuleFor(value => value.UserId).NotEmpty(); RuleFor(value => value.Password).NotEmpty().MaximumLength(256); }
}
public sealed class RequestAccountRecoveryValidator : AbstractValidator<RequestAccountRecoveryCommand>
{
    public RequestAccountRecoveryValidator() { RuleFor(value => value.AccountType).Must(AccountTypeInput.IsValid); RuleFor(value => value.Email).Must(EmailRules.IsValid); }
}
public sealed class RecoverAccountValidator : AbstractValidator<RecoverAccountCommand>
{
    public RecoverAccountValidator()
    {
        RuleFor(value => value.AccountType).Must(AccountTypeInput.IsValid); RuleFor(value => value.Email).Must(EmailRules.IsValid);
        RuleFor(value => value.Code).NotNull().Matches("^[0-9]{6}$");
    }
}
