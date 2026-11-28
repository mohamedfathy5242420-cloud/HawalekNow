namespace HawalekNow.Application.Modules.Identity.Contracts;

// Identity-owned facts for live authorization and administrative account review. No credentials/session data.
public sealed record AccountAccessInfo(Guid Id, string AccountType, bool EmailConfirmed, bool IsActive, string? UserName, string? Email);

public interface IAccountAccess
{
    Task<AccountAccessInfo?> FindAsync(Guid userId, CancellationToken cancellationToken);
    // Uses Identity's existing state lock; the caller must own the outer unit-of-work transaction.
    Task<AccountAccessInfo?> FindForMutationAsync(Guid userId, CancellationToken cancellationToken);
}
