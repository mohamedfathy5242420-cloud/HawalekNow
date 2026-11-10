using HawalekNow.Domain.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class RefreshCredential
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Digest { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class AccountSessionConfiguration : IEntityTypeConfiguration<AccountSession>
{
    public void Configure(EntityTypeBuilder<AccountSession> builder)
    {
        builder.ToTable("Sessions", "identity"); builder.HasKey(value => value.Id);
        builder.HasIndex(value => value.UserId);
        builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class RefreshCredentialConfiguration : IEntityTypeConfiguration<RefreshCredential>
{
    public void Configure(EntityTypeBuilder<RefreshCredential> builder)
    {
        builder.ToTable("RefreshCredentials", "identity"); builder.HasKey(value => value.Id);
        builder.Property(value => value.Digest).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasIndex(value => value.SessionId);
        builder.HasOne<AccountSession>().WithMany().HasForeignKey(value => value.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}
