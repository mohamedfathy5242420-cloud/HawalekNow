using HawalekNow.Domain.Modules.Merchants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed class StoreActivityChangeConfiguration : IEntityTypeConfiguration<StoreActivityChange>
{
    public void Configure(EntityTypeBuilder<StoreActivityChange> builder)
    {
        builder.ToTable("ActivityChanges", "merchants", table =>
        {
            table.HasCheckConstraint("CK_ActivityChanges_Status", "[Status] IN (1,2,3)");
            table.HasCheckConstraint("CK_ActivityChanges_Different", "[PreviousActivityId] <> [RequestedActivityId]");
            table.HasCheckConstraint("CK_ActivityChanges_Review", "([Status]=1 AND [ReviewedBy] IS NULL AND [ReviewedAt] IS NULL AND [ReviewReason] IS NULL) OR ([Status] IN (2,3) AND [ReviewedBy] IS NOT NULL AND [ReviewedAt] IS NOT NULL AND ([Status]=2 OR NULLIF(LTRIM(RTRIM([ReviewReason])), '') IS NOT NULL))");
        });
        builder.HasKey(value => value.Id); builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasOne<Store>().WithMany().HasForeignKey(value => value.StoreId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => value.StoreId).IsUnique().HasFilter("[Status] = 1");
        builder.HasIndex(value => new { value.Status, value.RequestedAt, value.Id });
        builder.HasIndex(value => value.PreviousActivityId); builder.HasIndex(value => value.RequestedActivityId);
        builder.Property(value => value.RequestReason).HasMaxLength(1000); builder.Property(value => value.ReviewReason).HasMaxLength(1000);
        // Cross-module Catalog FKs are primitive global migration DDL, not entity/navigation dependencies.
    }
}
