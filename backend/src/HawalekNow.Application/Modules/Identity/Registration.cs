using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using MediatR;

namespace HawalekNow.Application.Modules.Identity;

public sealed record RegisterCustomerCommand(string Username, string Email, string Password, bool AcceptTerms) : IRequest<Result>;
public sealed record RegisterMerchantCommand(string Username, string Email, string Password, bool AcceptTerms) : IRequest<Result>;
public sealed record RegistrationDetails(string Username, string Email, string Password, bool AcceptTerms);

public interface IAccountRegistration
{
    Task<Result> RegisterCustomerAsync(RegistrationDetails details, CancellationToken cancellationToken);
    Task<Result> RegisterMerchantAsync(RegistrationDetails details, CancellationToken cancellationToken);
}

public sealed class RegisterCustomerHandler(IAccountRegistration registration) : IRequestHandler<RegisterCustomerCommand, Result>
{
    public Task<Result> Handle(RegisterCustomerCommand request, CancellationToken cancellationToken) =>
        registration.RegisterCustomerAsync(new(request.Username, request.Email, request.Password, request.AcceptTerms), cancellationToken);
}

public sealed class RegisterMerchantHandler(IAccountRegistration registration) : IRequestHandler<RegisterMerchantCommand, Result>
{
    public Task<Result> Handle(RegisterMerchantCommand request, CancellationToken cancellationToken) =>
        registration.RegisterMerchantAsync(new(request.Username, request.Email, request.Password, request.AcceptTerms), cancellationToken);
}

public sealed class RegisterCustomerValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerValidator()
    {
        RuleFor(value => value.Username).NotEmpty().Length(3, 64).Matches("^[A-Za-z0-9._-]+$");
        RuleFor(value => value.Email).Must(EmailRules.IsValid);
        RuleFor(value => value.Password).Must(PasswordRules.IsAcceptable);
        RuleFor(value => value.AcceptTerms).Equal(true);
    }
}

public sealed class RegisterMerchantValidator : AbstractValidator<RegisterMerchantCommand>
{
    public RegisterMerchantValidator()
    {
        RuleFor(value => value.Username).NotEmpty().Length(3, 64).Matches("^[A-Za-z0-9._-]+$");
        RuleFor(value => value.Email).Must(EmailRules.IsValid);
        RuleFor(value => value.Password).Must(PasswordRules.IsAcceptable);
        RuleFor(value => value.AcceptTerms).Equal(true);
    }
}
