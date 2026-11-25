namespace HawalekNow.Domain.Modules.Catalog;

// Intentionally no foreign key: a later Admin deletion must not be undone by seed replay.
public sealed class ActivitySeedKey
{
    public Guid Id { get; private set; }
    public string SourceKey { get; private set; } = "";
    public Guid ActivityId { get; private set; }
    public string SourceVersion { get; private set; } = "";
    private ActivitySeedKey() { }
    public static ActivitySeedKey Record(string sourceKey, Guid activityId, string sourceVersion)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) || sourceKey.Length > 100 || activityId == Guid.Empty || string.IsNullOrWhiteSpace(sourceVersion) || sourceVersion.Length > 80) throw new ArgumentException("Invalid seed provenance.");
        return new() { Id = Guid.CreateVersion7(), SourceKey = sourceKey, ActivityId = activityId, SourceVersion = sourceVersion };
    }
}
