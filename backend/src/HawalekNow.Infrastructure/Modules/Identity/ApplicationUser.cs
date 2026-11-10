using HawalekNow.Domain.Modules.Identity;
using Microsoft.AspNetCore.Identity;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public AccountType AccountType { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

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
