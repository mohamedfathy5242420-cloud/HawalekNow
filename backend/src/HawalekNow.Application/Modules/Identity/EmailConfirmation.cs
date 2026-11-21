using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using MediatR;

namespace HawalekNow.Application.Modules.Identity;

public sealed record RequestEmailCodeCommand(string AccountType, string Email) : IRequest<Result>;
public sealed record VerifyEmailCodeCommand(string AccountType, string Email, string Code) : IRequest<Result>;
public interface IEmailConfirmation
{
    Task<Result> RequestAsync(string accountType, string email, CancellationToken cancellationToken);
    Task<Result> VerifyAsync(string accountType, string email, string code, CancellationToken cancellationToken);
}
public static class AccountTypeInput
{
    public static bool IsValid(string? value) => Enum.TryParse<AccountType>(value, true, out var parsed) && Enum.IsDefined(parsed);
}
public sealed class RequestEmailCodeHandler(IEmailConfirmation service) : IRequestHandler<RequestEmailCodeCommand, Result>
{
    public Task<Result> Handle(RequestEmailCodeCommand request, CancellationToken cancellationToken) => service.RequestAsync(request.AccountType, request.Email, cancellationToken);
}
public sealed class VerifyEmailCodeHandler(IEmailConfirmation service) : IRequestHandler<VerifyEmailCodeCommand, Result>
{
    public Task<Result> Handle(VerifyEmailCodeCommand request, CancellationToken cancellationToken) => service.VerifyAsync(request.AccountType, request.Email, request.Code, cancellationToken);
}
public sealed class RequestEmailCodeValidator : AbstractValidator<RequestEmailCodeCommand>
{
    public RequestEmailCodeValidator() { RuleFor(value => value.AccountType).Must(AccountTypeInput.IsValid); RuleFor(value => value.Email).Must(EmailRules.IsValid); }
}
public sealed class VerifyEmailCodeValidator : AbstractValidator<VerifyEmailCodeCommand>
{
    public VerifyEmailCodeValidator()
    {
        RuleFor(value => value.AccountType).Must(AccountTypeInput.IsValid); RuleFor(value => value.Email).Must(EmailRules.IsValid);
        RuleFor(value => value.Code).NotNull().Matches("^[0-9]{6}$");
    }
}
