using HawalekNow.Domain.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class OtpConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("OtpChallenges", "identity"); builder.HasKey(value => value.Id);
        builder.HasIndex(value => new { value.UserId, value.Purpose }).IsUnique();
        builder.Property(value => value.Digest).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(value => value.RowVersion).IsRowVersion();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
