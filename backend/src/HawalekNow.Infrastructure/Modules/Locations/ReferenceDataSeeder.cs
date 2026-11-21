using HawalekNow.Application.Modules.Catalog.Contracts;
using HawalekNow.Application.Modules.Locations;
using HawalekNow.Domain.Modules.Locations;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HawalekNow.Infrastructure.Modules.Locations;

public sealed class LocationSeedKeyConfiguration : IEntityTypeConfiguration<LocationSeedKey>
{
    public void Configure(EntityTypeBuilder<LocationSeedKey> builder)
    {
        builder.ToTable("SeedKeys", "locations", table => table.HasCheckConstraint("CK_LocationSeedKeys_Kind", "[Kind] IN (1,2,3)")); builder.HasKey(value => value.Id);
        builder.Property(value => value.SourceKey).HasMaxLength(32).UseCollation("Latin1_General_100_BIN2").IsRequired(); builder.HasIndex(value => value.SourceKey).IsUnique();
        builder.Property(value => value.SourceCommit).HasMaxLength(40).IsRequired();
    }
}
public sealed class ReferenceDataSeeder(HawalekDbContext context, IInitialActivities activities, LocationStateLock stateLock, IReferenceSeedSource source) : IReferenceDataSeeder
{
    public async Task<ReferenceSeedOutcome> SeedAsync(CancellationToken cancellationToken = default)
    {
        var dataset = source.Read(); ReferenceSeedRules.Validate(dataset);
        await using var transaction = await context.BeginAsync(cancellationToken);
        // Every cross-module seed follows one lock order: Catalog, then Locations.
        var catalog = await activities.SeedInsideTransactionAsync(cancellationToken); await stateLock.AcquireAsync(false, cancellationToken, 60000);
        var mappings = await context.Set<LocationSeedKey>().AsNoTracking().ToDictionaryAsync(value => value.SourceKey, cancellationToken);
        var governorates = 0; var cities = 0; var areas = 0;
        foreach (var place in dataset.Governorates)
        {
            if (mappings.ContainsKey(place.SourceKey)) continue;
            var item = Governorate.Create(place.NameAr, place.NameEn); context.Add(item);
            var receipt = LocationSeedKey.Record(place.SourceKey, SeedPlaceKind.Governorate, item.Id, dataset.SourceCommit); context.Add(receipt); mappings.Add(place.SourceKey, receipt); governorates++;
        }
        await context.SaveChangesAsync(cancellationToken);
        foreach (var place in dataset.Cities)
        {
            if (mappings.ContainsKey(place.SourceKey)) continue;
            var parent = mappings[place.ParentSourceKey!]; if (parent.Kind != SeedPlaceKind.Governorate) throw new InvalidOperationException("Invalid imported parent mapping.");
            var item = City.Create(parent.ReferenceId, place.NameAr, place.NameEn); context.Add(item);
            var receipt = LocationSeedKey.Record(place.SourceKey, SeedPlaceKind.City, item.Id, dataset.SourceCommit); context.Add(receipt); mappings.Add(place.SourceKey, receipt); cities++;
        }
        await context.SaveChangesAsync(cancellationToken);
        var cityParents = dataset.Cities.ToDictionary(value => value.SourceKey, value => value.ParentSourceKey!, StringComparer.Ordinal);
        foreach (var place in dataset.Areas)
        {
            if (mappings.ContainsKey(place.SourceKey)) continue;
            var parent = mappings[place.ParentSourceKey!]; if (parent.Kind != SeedPlaceKind.City) throw new InvalidOperationException("Invalid imported parent mapping.");
            var governorate = mappings[cityParents[place.ParentSourceKey!]];
            var item = Area.Create(governorate.ReferenceId, parent.ReferenceId, place.NameAr, place.NameEn); context.Add(item);
            var receipt = LocationSeedKey.Record(place.SourceKey, SeedPlaceKind.Area, item.Id, dataset.SourceCommit); context.Add(receipt); mappings.Add(place.SourceKey, receipt); areas++;
        }
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return new(catalog.Added, governorates, cities, areas, dataset.SourceCommit);
    }
}
