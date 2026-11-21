namespace HawalekNow.Application.Shared.Messaging;

public enum OutboxHandlingResult { Completed, Suppressed }
public interface IOutboxHandler
{
    string Kind { get; }
    Task<OutboxHandlingResult> HandleAsync(Guid messageId, string protectedPayload, CancellationToken cancellationToken);
}
