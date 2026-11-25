using System.Collections.Immutable;
using System.Text.RegularExpressions;
using HawalekNow.Domain.Modules.Locations;

namespace HawalekNow.Application.Modules.Locations;

public sealed record SeedPlace(string SourceKey, string? ParentSourceKey, string NameAr, string NameEn);
public sealed record ReferenceSeedDataset(string SourceCommit, ImmutableArray<SeedPlace> Governorates, ImmutableArray<SeedPlace> Cities, ImmutableArray<SeedPlace> Areas);
public sealed record ReferenceSeedOutcome(int ActivitiesAdded, int GovernoratesAdded, int CitiesAdded, int AreasAdded, string SourceCommit);
public interface IReferenceSeedSource { ReferenceSeedDataset Read(); }
public interface IReferenceDataSeeder { Task<ReferenceSeedOutcome> SeedAsync(CancellationToken cancellationToken = default); }
public static class ReferenceSeedRules
{
    private static readonly Regex SourceKeyPattern = new("^EG[0-9]{2,12}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    public const string AcceptedCommit = "76b47b22872288c0d8b0c757975980e6986945e8";
    public static void Validate(ReferenceSeedDataset source)
    {
        if (source is null || source.SourceCommit != AcceptedCommit || source.Governorates.IsDefaultOrEmpty || source.Cities.IsDefault || source.Areas.IsDefault || source.Governorates.Length + source.Cities.Length + source.Areas.Length > 50_000) throw Invalid();
        var all = source.Governorates.Concat(source.Cities).Concat(source.Areas).ToArray();
        if (all.Any(value => value is null || string.IsNullOrWhiteSpace(value.SourceKey) || !SourceKeyPattern.IsMatch(value.SourceKey)) || all.Select(value => value.SourceKey).Distinct(StringComparer.Ordinal).Count() != all.Length) throw Invalid();
        try { foreach (var place in all) { _ = LocationNames.Validate(place.NameAr); _ = LocationNames.Validate(place.NameEn); } }
        catch (ArgumentException) { throw Invalid(); }
        var governorates = source.Governorates.Select(value => value.SourceKey).ToHashSet(StringComparer.Ordinal);
        var cities = source.Cities.Select(value => value.SourceKey).ToHashSet(StringComparer.Ordinal);
        if (source.Governorates.Any(value => value.ParentSourceKey is not null) || source.Cities.Any(value => value.ParentSourceKey is null || !governorates.Contains(value.ParentSourceKey)) || source.Areas.Any(value => value.ParentSourceKey is null || !cities.Contains(value.ParentSourceKey))) throw Invalid();
    }
    private static InvalidOperationException Invalid() => new("The pinned reference dataset is invalid.");
}
