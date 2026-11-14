namespace HawalekNow.Domain.Modules.Identity;

public sealed class AccountSession
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastRefreshedAt { get; private set; }
    public DateTimeOffset RefreshExpiresAt { get; private set; }
    public DateTimeOffset AbsoluteExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private AccountSession() { }
    public static AccountSession Create(Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Invalid session owner.");
        return new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CreatedAt = now,
            LastRefreshedAt = now,
            RefreshExpiresAt = now.AddDays(7),
            AbsoluteExpiresAt = now.AddDays(30)
        };
    }
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && RefreshExpiresAt > now && AbsoluteExpiresAt > now;
    public bool Rotate(DateTimeOffset now)
    {
        if (!IsActive(now)) return false;
        LastRefreshedAt = now;
        RefreshExpiresAt = now.AddDays(7) < AbsoluteExpiresAt ? now.AddDays(7) : AbsoluteExpiresAt;
        return true;
    }
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
