using HawalekNow.Infrastructure.Shared.Persistence;

namespace HawalekNow.Infrastructure.Shared.Messaging;

public sealed class OutboxWriter(HawalekDbContext context, TimeProvider clock)
{
    public Guid Add(string kind, string protectedPayload)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Outbox intent requires the owning SQL transaction.");
        var message = new OutboxMessage { Kind = kind, ProtectedPayload = protectedPayload, CreatedAt = clock.GetUtcNow(), NextAttemptAt = clock.GetUtcNow() };
        context.Add(message); return message.Id;
    }
}
