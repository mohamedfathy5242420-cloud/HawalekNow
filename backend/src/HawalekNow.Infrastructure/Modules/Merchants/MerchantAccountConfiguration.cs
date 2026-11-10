using HawalekNow.Domain.Modules.Merchants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed class MerchantAccountConfiguration : IEntityTypeConfiguration<MerchantAccount>
{
    public void Configure(EntityTypeBuilder<MerchantAccount> builder)
    {
        builder.ToTable("Accounts", "merchants", table => table.HasCheckConstraint("CK_MerchantAccounts_Status", "[Status] IN (1,2,3,4,5)"));
        builder.HasKey(account => account.Id);
        builder.HasIndex(account => account.UserId).IsUnique();
        builder.Property(account => account.RowVersion).IsRowVersion();
        builder.Ignore(account => account.CanPrepareAccount);
        // UserId is an opaque owner id; no cross-module entity/navigation dependency.
    }
}
