using System.Security.Cryptography;
using System.Text;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;

namespace HawalekNow.Infrastructure.Modules.Identity;

public enum IdentifierKind { Username = 1, Email = 2 }

public sealed class IdentifierDigests
{
    private readonly byte[] key;
    public IdentifierDigests(IConfiguration configuration)
    {
        try
        {
            key = Convert.FromBase64String(configuration["Identity:IdentifierKey"] ?? "");
            if (key.Length != 32) throw new FormatException();
        }
        catch (FormatException) { throw new InvalidOperationException("Configure a stable 32-byte Identity:IdentifierKey outside source control."); }
    }

    public string Hash(AccountType type, IdentifierKind kind, string normalizedValue) =>
        Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{(int)type}:{(int)kind}:{normalizedValue}")));
}

// These rows survive permanent deletion. Original usernames/emails and credentials are never stored here.
public sealed class ReservedIdentifier
{
    public Guid Id { get; private set; }
    public AccountType AccountType { get; private set; }
    public IdentifierKind Kind { get; private set; }
    public string Digest { get; private set; } = "";
    private ReservedIdentifier() { }
    public static ReservedIdentifier Create(AccountType type, IdentifierKind kind, string digest) =>
        new() { Id = Guid.CreateVersion7(), AccountType = type, Kind = kind, Digest = digest };
}

public sealed class ReservedIdentifierConfiguration : IEntityTypeConfiguration<ReservedIdentifier>
{
    public void Configure(EntityTypeBuilder<ReservedIdentifier> builder)
    {
        builder.ToTable("ReservedIdentifiers", "identity");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Digest).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.HasIndex(value => new { value.AccountType, value.Kind, value.Digest }).IsUnique();
    }
}

// Registration and permanent purge acquire the same sorted locks before checking or changing identifiers.
public sealed class IdentifierLocks(HawalekDbContext context, IdentifierDigests digests)
{
    public async Task AcquireAsync(AccountType type, string normalizedName, string normalizedEmail, CancellationToken cancellationToken)
    {
        var resources = new[] { digests.Hash(type, IdentifierKind.Username, normalizedName), digests.Hash(type, IdentifierKind.Email, normalizedEmail) };
        foreach (var resource in resources.Order(StringComparer.Ordinal))
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
                IF @result < 0 THROW 51002, N'Account transaction lock unavailable.', 1;
                """, cancellationToken);
        }
    }
}
