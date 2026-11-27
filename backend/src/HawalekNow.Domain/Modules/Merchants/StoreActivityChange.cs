namespace HawalekNow.Domain.Modules.Merchants;

public enum ActivityChangeStatus { Pending = 1, Approved = 2, Rejected = 3 }

public sealed class StoreActivityChange
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid PreviousActivityId { get; private set; }
    public Guid RequestedActivityId { get; private set; }
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public string? RequestReason { get; private set; }
    public ActivityChangeStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private StoreActivityChange() { }
    public static StoreActivityChange Create(Guid storeId, Guid previousActivityId, Guid requestedActivityId, Guid actorId, string? reason, DateTimeOffset now)
    {
        if (storeId == Guid.Empty || previousActivityId == Guid.Empty || requestedActivityId == Guid.Empty || previousActivityId == requestedActivityId || actorId == Guid.Empty || now == default || !ValidReason(reason))
            throw new ArgumentException("Invalid store activity request.");
        return new()
        {
            Id = Guid.CreateVersion7(),
            StoreId = storeId,
            PreviousActivityId = previousActivityId,
            RequestedActivityId = requestedActivityId,
            RequestedBy = actorId,
            RequestedAt = now.ToUniversalTime(),
            RequestReason = Clean(reason),
            Status = ActivityChangeStatus.Pending
        };
    }
    public bool TryReview(bool approve, Guid actorId, string? reason, DateTimeOffset now)
    {
        if (Status != ActivityChangeStatus.Pending || actorId == Guid.Empty || now == default || !ValidReason(reason) || !approve && string.IsNullOrWhiteSpace(reason)) return false;
        Status = approve ? ActivityChangeStatus.Approved : ActivityChangeStatus.Rejected; ReviewedBy = actorId; ReviewedAt = now.ToUniversalTime(); ReviewReason = Clean(reason); return true;
    }
    public static bool ValidReason(string? value) => value is null || value.Length <= 1000 && !value.Contains('\0');
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
