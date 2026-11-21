using HawalekNow.Domain.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class AccountDeletionConfiguration : IEntityTypeConfiguration<AccountDeletion>, IEntityTypeConfiguration<AccountDeletionAudit>
{
    public void Configure(EntityTypeBuilder<AccountDeletion> builder)
    {
        builder.ToTable("AccountDeletions", "identity"); builder.HasKey(value => value.Id);
        builder.HasIndex(value => value.UserId).IsUnique(); builder.HasIndex(value => value.PurgeAt);
        builder.Property(value => value.ScheduledJobId).HasMaxLength(100);
        builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<AccountDeletionAudit> builder)
    {
        builder.ToTable("AccountDeletionAudit", "identity"); builder.HasKey(value => value.Id);
        builder.HasIndex(value => new { value.UserId, value.Generation, value.Kind }).IsUnique();
        // Deliberately no user FK: only technical IDs, transition kind and UTC survive a permanent purge.
    }
}
