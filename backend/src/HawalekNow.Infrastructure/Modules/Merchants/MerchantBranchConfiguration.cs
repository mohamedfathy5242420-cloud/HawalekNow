using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed class MerchantBranchConfiguration : IEntityTypeConfiguration<MerchantBranch>
{
    public void Configure(EntityTypeBuilder<MerchantBranch> builder)
    {
        builder.ToTable("Branches", "merchants", table =>
        {
            table.HasCheckConstraint("CK_Branches_Kind", "[Kind] IN (1,2)");
            table.HasCheckConstraint("CK_Branches_PublicAddress", "[PublicFixedAddressConfirmed] = 1 AND LEN(LTRIM(RTRIM([Name]))) > 0 AND LEN(LTRIM(RTRIM([Address]))) > 0");
            table.HasCheckConstraint("CK_Branches_Position", "[Position].STSrid = 4326 AND [Position].STGeometryType() = 'Point' AND [Position].STIsEmpty() = 0");
        });
        builder.HasKey(value => value.Id); builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasOne<Store>().WithMany().HasForeignKey(value => value.StoreId).OnDelete(DeleteBehavior.Cascade); builder.HasIndex(value => new { value.StoreId, value.Id });
        builder.Property(value => value.Name).HasMaxLength(200).IsRequired(); builder.Property(value => value.Address).HasMaxLength(600).IsRequired();
        builder.Property(value => value.Phone).HasMaxLength(16).IsUnicode(false); builder.Property(value => value.WhatsApp).HasMaxLength(16).IsUnicode(false);
        builder.Property(value => value.Position).HasGeographyPoint(); builder.HasIndex(value => new { value.AreaId, value.CityId, value.GovernorateId });
        // The global migration adds the opaque cross-schema administrative FK; no Location entity/navigation leaks here.
    }
}
