using HawalekNow.Domain.Modules.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Modules.Catalog;

public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("Activities", "catalog", table =>
            table.HasCheckConstraint("CK_Activities_Names", "LEN([NameAr]) > 0 AND LEN([NameEn]) > 0 AND LEN([NameArKey]) > 0 AND LEN([NameEnKey]) > 0"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.NameAr).HasMaxLength(100).IsRequired();
        builder.Property(value => value.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(value => value.NameArKey).HasMaxLength(160).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(value => value.NameEnKey).HasMaxLength(160).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.HasIndex(value => value.NameArKey).IsUnique();
        builder.HasIndex(value => value.NameEnKey).IsUnique();
        builder.Property(value => value.RowVersion).IsRowVersion();
    }
}

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", "catalog", table =>
        {
            table.HasCheckConstraint("CK_Categories_Parent", "[ParentId] IS NULL OR [ParentId] <> [Id]");
            table.HasCheckConstraint("CK_Categories_Names", "LEN([NameAr]) > 0 AND LEN([NameEn]) > 0 AND LEN([NameArKey]) > 0 AND LEN([NameEnKey]) > 0");
        });
        builder.HasKey(value => value.Id);
        builder.HasAlternateKey(value => new { value.Id, value.ActivityId });
        builder.Property(value => value.NameAr).HasMaxLength(100).IsRequired();
        builder.Property(value => value.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(value => value.NameArKey).HasMaxLength(160).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(value => value.NameEnKey).HasMaxLength(160).UseCollation("Latin1_General_100_BIN2").IsRequired();
        builder.Property(value => value.RowVersion).IsRowVersion();
        // Do not let EF rewrite tracked children to roots when a parent is deleted.
        builder.HasOne<Activity>().WithMany().HasForeignKey(value => value.ActivityId).OnDelete(DeleteBehavior.ClientNoAction);
        builder.HasOne<Category>().WithMany().HasForeignKey(value => new { value.ParentId, value.ActivityId })
            .HasPrincipalKey(value => new { value.Id, value.ActivityId }).OnDelete(DeleteBehavior.ClientNoAction);
        builder.HasIndex(value => new { value.ActivityId, value.NameArKey }).IsUnique().HasFilter("[ParentId] IS NULL");
        builder.HasIndex(value => new { value.ActivityId, value.NameEnKey }).IsUnique().HasFilter("[ParentId] IS NULL");
        builder.HasIndex(value => new { value.ActivityId, value.ParentId, value.NameArKey }).IsUnique().HasFilter("[ParentId] IS NOT NULL");
        builder.HasIndex(value => new { value.ActivityId, value.ParentId, value.NameEnKey }).IsUnique().HasFilter("[ParentId] IS NOT NULL");
    }
}
