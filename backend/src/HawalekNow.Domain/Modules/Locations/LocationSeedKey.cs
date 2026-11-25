namespace HawalekNow.Domain.Modules.Locations;

public enum SeedPlaceKind { Governorate = 1, City = 2, Area = 3 }

// Receipts survive intentional Admin deletion and prevent resurrection on replay.
public sealed class LocationSeedKey
{
    public Guid Id { get; private set; }
    public string SourceKey { get; private set; } = "";
    public SeedPlaceKind Kind { get; private set; }
    public Guid ReferenceId { get; private set; }
    public string SourceCommit { get; private set; } = "";
    private LocationSeedKey() { }
    public static LocationSeedKey Record(string sourceKey, SeedPlaceKind kind, Guid referenceId, string sourceCommit)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) || sourceKey.Length > 32 || !Enum.IsDefined(kind) || referenceId == Guid.Empty || string.IsNullOrEmpty(sourceCommit) || sourceCommit.Length != 40 || !sourceCommit.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid seed provenance.");
        return new() { Id = Guid.CreateVersion7(), SourceKey = sourceKey, Kind = kind, ReferenceId = referenceId, SourceCommit = sourceCommit };
    }
}
