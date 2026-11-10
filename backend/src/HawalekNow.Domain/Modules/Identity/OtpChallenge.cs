namespace HawalekNow.Domain.Modules.Identity;

public enum OtpPurpose { EmailConfirmation = 1, PasswordReset = 2, AccountRecovery = 3 }

public sealed class OtpChallenge
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public OtpPurpose Purpose { get; private set; }
    public Guid Generation { get; private set; }
    public string Digest { get; private set; } = "";
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? LastSentAt { get; private set; }
    public DateTimeOffset WindowStartedAt { get; private set; }
    public int SentInWindow { get; private set; }
    public int FailedAttempts { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private OtpChallenge() { }

    public static OtpChallenge Create(Guid userId, OtpPurpose purpose)
    {
        if (userId == Guid.Empty || !Enum.IsDefined(purpose)) throw new ArgumentException("Invalid challenge owner or purpose.");
        return new() { Id = Guid.CreateVersion7(), UserId = userId, Purpose = purpose };
    }

    public bool Issue(Guid generation, string digest, DateTimeOffset now)
    {
        Unlock(now);
        if (LockedUntil > now || LastSentAt + TimeSpan.FromSeconds(60) > now) return false;
        if (WindowStartedAt + TimeSpan.FromMinutes(15) <= now) { WindowStartedAt = now; SentInWindow = 0; }
        if (SentInWindow >= 3) return false;
        Generation = generation; Digest = digest; ExpiresAt = now.AddMinutes(10); ConsumedAt = null;
        LastSentAt = now; SentInWindow++; // FailedAttempts intentionally survives resend.
        return true;
    }

    public bool Consume(bool matches, DateTimeOffset now)
    {
        Unlock(now);
        if (ConsumedAt is not null || Generation == Guid.Empty || ExpiresAt <= now || LockedUntil > now) return false;
        if (!matches)
        {
            FailedAttempts++;
            if (FailedAttempts >= 5) LockedUntil = now.AddMinutes(30);
            return false;
        }
        ConsumedAt = now; FailedAttempts = 0;
        return true;
    }

    private void Unlock(DateTimeOffset now)
    {
        if (LockedUntil is not null && LockedUntil <= now) { LockedUntil = null; FailedAttempts = 0; }
    }
}
