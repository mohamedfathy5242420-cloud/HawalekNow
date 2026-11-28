using HawalekNow.Domain.Shared.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Shared.Auditing;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries", "operations");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Operation).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.TargetKind).HasMaxLength(40).IsRequired();
        builder.Property(entry => entry.Reason).HasMaxLength(1000);
        builder.Property(entry => entry.PreviousState).HasMaxLength(40);
        builder.Property(entry => entry.CurrentState).HasMaxLength(40);
        builder.HasIndex(entry => new { entry.TargetKind, entry.TargetId, entry.OccurredAt });
        builder.HasIndex(entry => new { entry.ActorId, entry.OccurredAt });
        // No actor/target FK: required history survives account/profile deletion.
    }
}
