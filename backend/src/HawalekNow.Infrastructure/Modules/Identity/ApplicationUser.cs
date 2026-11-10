using HawalekNow.Domain.Modules.Identity;
using Microsoft.AspNetCore.Identity;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public AccountType AccountType { get; private set; }
    public AccountAccessStatus AccessStatus { get; private set; } = AccountAccessStatus.Active;
    public byte[] RowVersion { get; private set; } = [];
    public DateTimeOffset? TermsAcceptedAt { get; private set; }

    public void RecordTermsAcceptance(DateTimeOffset acceptedAt) => TermsAcceptedAt = acceptedAt.ToUniversalTime();

    private ApplicationUser() { }

    public static ApplicationUser Create(AccountType accountType, string username, string email)
    {
        if (!Enum.IsDefined(accountType)) throw new ArgumentOutOfRangeException(nameof(accountType));
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return new()
        {
            Id = Guid.CreateVersion7(),
            AccountType = accountType,
            UserName = username.Trim(),
            Email = email.Trim(),
            SecurityStamp = Guid.CreateVersion7().ToString("N"),
            ConcurrencyStamp = Guid.CreateVersion7().ToString("N")
        };
    }
}
