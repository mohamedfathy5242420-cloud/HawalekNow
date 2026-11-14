namespace HawalekNow.Domain.Modules.Identity;

public sealed class AccountDeletion
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid Generation { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset PurgeAt { get; private set; }
    public DateTimeOffset? RestoredAt { get; private set; }
    public string? ScheduledJobId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private AccountDeletion() { }
    public static AccountDeletion Create(Guid userId, AccountType type, DateTimeOffset now)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Invalid deletion owner.");
        var value = new AccountDeletion { Id = Guid.CreateVersion7(), UserId = userId, RestoredAt = now };
        value.Request(type, now); return value;
    }
    public bool Request(AccountType type, DateTimeOffset now)
    {
        if (type is not AccountType.Customer and not AccountType.Merchant) throw new InvalidOperationException("Self deletion is unavailable.");
        if (RestoredAt is null) return false;
        Generation = Guid.CreateVersion7(); RequestedAt = now; PurgeAt = now.AddDays(30); RestoredAt = null; ScheduledJobId = null; return true;
    }
    public bool CanRecover(DateTimeOffset now) => RestoredAt is null && now < PurgeAt;
    public bool Restore(DateTimeOffset now)
    {
        if (!CanRecover(now)) return false;
        RestoredAt = now; return true;
    }
    public bool CanPurge(Guid generation, DateTimeOffset now) => RestoredAt is null && Generation == generation && now >= PurgeAt;
    public void RecordSchedule(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        if (jobId.Length > 100) throw new ArgumentException("Invalid schedule identifier.");
        ScheduledJobId = jobId;
    }
}

public enum AccountDeletionEvent { Requested = 1, Recovered = 2, Purged = 3 }
public sealed class AccountDeletionAudit
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid Generation { get; private set; }
    public AccountDeletionEvent Kind { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    private AccountDeletionAudit() { }
    public static AccountDeletionAudit Create(Guid userId, Guid generation, AccountDeletionEvent kind, DateTimeOffset now) =>
        new() { Id = Guid.CreateVersion7(), UserId = userId, Generation = generation, Kind = kind, OccurredAt = now };
}
