using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class IdentityModelConfiguration : IEntityTypeConfiguration<ApplicationUser>,
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>, IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users", "identity", table => table.HasCheckConstraint("CK_Users_AccountType", "[AccountType] IN (1,2,3)"));
        builder.HasKey(user => user.Id);
        builder.Property(user => user.UserName).HasMaxLength(64).IsRequired();
        builder.Property(user => user.NormalizedUserName).HasMaxLength(64).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(user => user.Email).HasMaxLength(254).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(254).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(user => user.ConcurrencyStamp).IsConcurrencyToken();
        builder.Property(user => user.RowVersion).IsRowVersion();
        builder.HasIndex(user => new { user.AccountType, user.NormalizedUserName }).IsUnique();
        builder.HasIndex(user => new { user.AccountType, user.NormalizedEmail }).IsUnique();
        builder.HasMany<IdentityUserClaim<Guid>>().WithOne().HasForeignKey(claim => claim.UserId).IsRequired();
        builder.HasMany<IdentityUserLogin<Guid>>().WithOne().HasForeignKey(login => login.UserId).IsRequired();
        builder.HasMany<IdentityUserToken<Guid>>().WithOne().HasForeignKey(token => token.UserId).IsRequired();
    }

    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("UserClaims", "identity");
        builder.HasKey(claim => claim.Id);
    }

    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("UserLogins", "identity");
        builder.HasKey(login => new { login.LoginProvider, login.ProviderKey });
        builder.Property(login => login.LoginProvider).HasMaxLength(128);
        builder.Property(login => login.ProviderKey).HasMaxLength(128);
    }

    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("UserTokens", "identity");
        builder.HasKey(token => new { token.UserId, token.LoginProvider, token.Name });
        builder.Property(token => token.LoginProvider).HasMaxLength(128);
        builder.Property(token => token.Name).HasMaxLength(128);
    }
}
