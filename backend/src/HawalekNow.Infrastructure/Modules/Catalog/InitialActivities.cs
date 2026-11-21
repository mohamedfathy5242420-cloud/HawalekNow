using System.Collections.Immutable;
using HawalekNow.Application.Modules.Catalog.Contracts;
using HawalekNow.Domain.Modules.Catalog;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Modules.Catalog;

public sealed class ActivitySeedKeyConfiguration : IEntityTypeConfiguration<ActivitySeedKey>
{
    public void Configure(EntityTypeBuilder<ActivitySeedKey> builder)
    {
        builder.ToTable("SeedKeys", "catalog"); builder.HasKey(value => value.Id);
        builder.Property(value => value.SourceKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2").IsRequired(); builder.HasIndex(value => value.SourceKey).IsUnique();
        builder.Property(value => value.SourceVersion).HasMaxLength(80).IsRequired();
    }
}
public sealed class InitialActivities(HawalekDbContext context, CatalogTreeLock treeLock) : IInitialActivities
{
    private static readonly (string Key, string Ar, string En)[] Definitions =
    [
        ("activity.clothing-accessories", "ملابس وإكسسوارات", "Clothing and accessories"),
        ("activity.electronics-mobile", "إلكترونيات وموبايلات", "Electronics and mobile phones"),
        ("activity.furniture-household", "أثاث وأدوات منزلية", "Furniture and household goods"),
        ("activity.car-parts", "قطع غيار سيارات", "Car spare parts")
    ];
    public async Task<InitialActivitiesOutcome> SeedInsideTransactionAsync(CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Reference seed requires the caller's unit of work transaction.");
        await treeLock.AcquireAsync(cancellationToken, 60000);
        var mappings = await context.Set<ActivitySeedKey>().AsNoTracking().ToDictionaryAsync(value => value.SourceKey, value => value.ActivityId, cancellationToken);
        var added = 0;
        foreach (var (key, ar, en) in Definitions)
        {
            if (mappings.ContainsKey(key)) continue;
            var arKey = CatalogNames.Key(ar); var enKey = CatalogNames.Key(en);
            var activity = await context.Set<Activity>().SingleOrDefaultAsync(value => value.NameArKey == arKey && value.NameEnKey == enKey, cancellationToken);
            if (activity is null) { activity = Activity.Create(ar, en); context.Add(activity); added++; }
            context.Add(ActivitySeedKey.Record(key, activity.Id, "approved-requirements:section-5:v1")); mappings.Add(key, activity.Id);
        }
        await context.SaveChangesAsync(cancellationToken);
        return new(added, mappings.ToImmutableDictionary(StringComparer.Ordinal));
    }
}
