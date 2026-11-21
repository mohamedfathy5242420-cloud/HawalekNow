using HawalekNow.Domain.Modules.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Modules.Locations;

public sealed class GovernorateConfiguration : IEntityTypeConfiguration<Governorate>
{
    public void Configure(EntityTypeBuilder<Governorate> builder)
    {
        builder.ToTable("Governorates", "locations", table => table.HasCheckConstraint("CK_Governorates_Names", "LEN([NameAr]) > 0 AND LEN([NameEn]) > 0"));
        builder.HasKey(value => value.Id); builder.Property(value => value.NameAr).HasMaxLength(160).IsRequired(); builder.Property(value => value.NameEn).HasMaxLength(160).IsRequired();
        builder.Property(value => value.RowVersion).IsRowVersion();
    }
}
public sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("Cities", "locations", table => table.HasCheckConstraint("CK_Cities_Names", "LEN([NameAr]) > 0 AND LEN([NameEn]) > 0"));
        builder.HasKey(value => value.Id); builder.HasAlternateKey(value => new { value.Id, value.GovernorateId });
        builder.Property(value => value.NameAr).HasMaxLength(160).IsRequired(); builder.Property(value => value.NameEn).HasMaxLength(160).IsRequired();
        builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasOne<Governorate>().WithMany().HasForeignKey(value => value.GovernorateId).OnDelete(DeleteBehavior.ClientNoAction);
        builder.HasIndex(value => new { value.GovernorateId, value.Id });
    }
}
public sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Areas", "locations", table => table.HasCheckConstraint("CK_Areas_Names", "LEN([NameAr]) > 0 AND LEN([NameEn]) > 0"));
        builder.HasKey(value => value.Id); builder.Property(value => value.NameAr).HasMaxLength(160).IsRequired(); builder.Property(value => value.NameEn).HasMaxLength(160).IsRequired();
        builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasOne<City>().WithMany().HasForeignKey(value => new { value.CityId, value.GovernorateId })
            .HasPrincipalKey(value => new { value.Id, value.GovernorateId }).OnDelete(DeleteBehavior.ClientNoAction);
        builder.HasIndex(value => new { value.GovernorateId, value.CityId, value.Id });
    }
}
