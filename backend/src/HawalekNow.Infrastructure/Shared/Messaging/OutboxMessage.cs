using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Shared.Messaging;

public enum OutboxState { Pending = 1, Processing = 2, Sent = 3, Suppressed = 4, Failed = 5 }
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Kind { get; set; } = "";
    public string ProtectedPayload { get; set; } = "";
    public Guid? SubjectId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public OutboxState State { get; set; } = OutboxState.Pending;
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int Attempts { get; set; }
    public string? LastFailureCode { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("Outbox", "operations"); builder.HasKey(value => value.Id);
        builder.Property(value => value.Kind).HasMaxLength(100).IsRequired();
        builder.Property(value => value.ProtectedPayload).IsRequired();
        builder.Property(value => value.LastFailureCode).HasMaxLength(40);
        builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasIndex(value => new { value.State, value.NextAttemptAt });
        builder.HasIndex(value => value.SubjectId);
    }
}
