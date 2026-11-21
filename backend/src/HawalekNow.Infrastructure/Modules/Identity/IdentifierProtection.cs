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
    public string Fingerprint => Convert.ToHexString(HMACSHA256.HashData(key, "HawalekNow.identifier-key.v1"u8));
}

public sealed class IdentifierKeyBinding
{
    public int Id { get; set; } = 1;
    public string Fingerprint { get; set; } = "";
}
public sealed class IdentifierKeyBindingConfiguration : IEntityTypeConfiguration<IdentifierKeyBinding>
{
    public void Configure(EntityTypeBuilder<IdentifierKeyBinding> builder)
    {
        builder.ToTable("IdentifierKeyBinding", "identity", table => table.HasCheckConstraint("CK_IdentifierKeyBinding_Singleton", "[Id] = 1"));
        builder.HasKey(value => value.Id); builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.Fingerprint).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
    }
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
        await EnsureKeyBindingAsync(cancellationToken);
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
    public async Task EnsureKeyBindingAsync(CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Identifier protection requires the owning transaction.");
        var binding = await context.Set<IdentifierKeyBinding>().AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (binding is null)
        {
            await context.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'identity.key-binding.v1', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
                IF @result < 0 THROW 51002, N'Account transaction lock unavailable.', 1;
                """, cancellationToken);
            binding = await context.Set<IdentifierKeyBinding>().AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (binding is null)
            {
                if (await context.Set<ReservedIdentifier>().AnyAsync(cancellationToken)) throw new InvalidOperationException("Identifier key binding requires operator reconciliation.");
                binding = new() { Fingerprint = digests.Fingerprint }; context.Add(binding); await context.SaveChangesAsync(cancellationToken);
            }
        }
        if (binding.Fingerprint != digests.Fingerprint) throw new InvalidOperationException("Identifier key does not match the durable binding.");
    }
}
