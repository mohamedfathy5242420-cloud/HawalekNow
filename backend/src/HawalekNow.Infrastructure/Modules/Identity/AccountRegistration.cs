using HawalekNow.Application.Modules.Identity;
using HawalekNow.Application.Modules.Merchants.Contracts;
using HawalekNow.Application.Shared.Persistence;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class AccountRegistration(UserManager<ApplicationUser> manager, IUnitOfWork work,
    IdentifierLocks locks, IMerchantAccounts merchants, TimeProvider clock) : IAccountRegistration
{
    public Task<Result> RegisterCustomerAsync(RegistrationDetails details, CancellationToken cancellationToken) =>
        RegisterAsync(AccountType.Customer, details, cancellationToken);
    public Task<Result> RegisterMerchantAsync(RegistrationDetails details, CancellationToken cancellationToken) =>
        RegisterAsync(AccountType.Merchant, details, cancellationToken);

    private async Task<Result> RegisterAsync(AccountType type, RegistrationDetails details, CancellationToken cancellationToken)
    {
        if (!details.AcceptTerms) return Result.Failure(new(ErrorKind.Validation));
        try
        {
            await using var transaction = await work.BeginAsync(cancellationToken);
            var user = ApplicationUser.Create(type, details.Username, details.Email);
            user.LockoutEnabled = true;
            user.RecordTermsAcceptance(clock.GetUtcNow());
            await locks.AcquireAsync(type, manager.NormalizeName(user.UserName!)!, manager.NormalizeEmail(user.Email!)!, cancellationToken);
            var result = await manager.CreateAsync(user, details.Password);
            // A valid registration request receives the same acknowledgement for existing or reserved identifiers.
            if (!result.Succeeded) return Result.Success();
            if (type == AccountType.Merchant) await merchants.CreatePendingAsync(user.Id, cancellationToken);
            await work.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Result.Success();
        }
    }
}
