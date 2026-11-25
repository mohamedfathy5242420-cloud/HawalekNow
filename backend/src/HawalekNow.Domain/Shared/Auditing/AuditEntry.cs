namespace HawalekNow.Domain.Shared.Auditing;

// Explicit business metadata only. Never serialize an entity, request or exception here.
public sealed class AuditEntry
{
    public Guid Id { get; private set; }
    public Guid ActorId { get; private set; }
    public string Operation { get; private set; } = "";
    public string TargetKind { get; private set; } = "";
    public Guid TargetId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? PreviousState { get; private set; }
    public string? CurrentState { get; private set; }

    private AuditEntry() { }

    public static AuditEntry Record(Guid actorId, string operation, string targetKind, Guid targetId, string? reason,
        DateTimeOffset occurredAt, string? previousState = null, string? currentState = null)
    {
        if (actorId == Guid.Empty || targetId == Guid.Empty || occurredAt == default)
            throw new ArgumentException("Audit identity and time are required.");
        if (!Code(operation, 80) || !Code(targetKind, 40) || previousState?.Length > 40 || currentState?.Length > 40)
            throw new ArgumentException("Invalid audit metadata.");
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason?.Length > 1000 || reason?.Contains('\0') == true) throw new ArgumentException("Invalid audit reason.");
        return new()
        {
            Id = Guid.CreateVersion7(),
            ActorId = actorId,
            Operation = operation,
            TargetKind = targetKind,
            TargetId = targetId,
            Reason = reason,
            OccurredAt = occurredAt.ToUniversalTime(),
            PreviousState = previousState,
            CurrentState = currentState
        };
    }

    private static bool Code(string value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
}
