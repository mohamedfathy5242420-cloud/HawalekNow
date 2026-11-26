using HawalekNow.Domain.Modules.Merchants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("Stores", "merchants", table =>
        {
            table.HasCheckConstraint("CK_Stores_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            table.HasCheckConstraint("CK_Stores_Contact", "NULLIF(LTRIM(RTRIM([Phone])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM([WhatsApp])), '') IS NOT NULL");
        });
        builder.HasKey(value => value.Id); builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasIndex(value => value.MerchantAccountId).IsUnique();
        builder.HasOne<MerchantAccount>().WithOne().HasForeignKey<Store>(value => value.MerchantAccountId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(value => value.Name).HasMaxLength(200).IsRequired();
        builder.Property(value => value.Phone).HasMaxLength(16).IsUnicode(false); builder.Property(value => value.WhatsApp).HasMaxLength(16).IsUnicode(false);
        builder.Property(value => value.Facebook).HasMaxLength(500); builder.Property(value => value.Instagram).HasMaxLength(500); builder.Property(value => value.TikTok).HasMaxLength(500);
        builder.Property(value => value.LogoKey).HasMaxLength(32).IsUnicode(false);
        builder.HasIndex(value => value.LogoKey).IsUnique().HasFilter("[LogoKey] IS NOT NULL"); builder.HasIndex(value => value.ActivityId);
        // Cross-module activity deletion protection is primitive DDL in the global migration, not an entity dependency.
    }
}
