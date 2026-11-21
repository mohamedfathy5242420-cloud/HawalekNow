using Hangfire;
using HawalekNow.Application.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Shared.Messaging;

public sealed class OutboxPump(HawalekDbContext context, IEnumerable<IOutboxHandler> handlers, TimeProvider clock)
{
    [Queue("identity")]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            for (var count = 0; count < 25; count++)
            {
                var message = await ClaimAsync(cancellationToken);
                if (message is null) return;
                try
                {
                    var handler = handlers.Single(value => value.Kind == message.Kind);
                    var result = await handler.HandleAsync(message.Id, message.ProtectedPayload, cancellationToken);
                    await context.Set<OutboxMessage>().Where(value => value.Id == message.Id && value.LeaseId == message.LeaseId && value.State == OutboxState.Processing)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.State, result == OutboxHandlingResult.Completed ? OutboxState.Sent : OutboxState.Suppressed)
                            .SetProperty(value => value.ProtectedPayload, "").SetProperty(value => value.LeaseId, (Guid?)null).SetProperty(value => value.LeaseUntil, (DateTimeOffset?)null)
                            .SetProperty(value => value.LastFailureCode, (string?)null), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    var retryAt = clock.GetUtcNow().AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(message.Attempts, 6))));
                    await context.Set<OutboxMessage>().Where(value => value.Id == message.Id && value.LeaseId == message.LeaseId && value.State == OutboxState.Processing)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.State, message.Attempts >= 10 ? OutboxState.Failed : OutboxState.Pending)
                            .SetProperty(value => value.NextAttemptAt, retryAt).SetProperty(value => value.LeaseId, (Guid?)null)
                            .SetProperty(value => value.LeaseUntil, (DateTimeOffset?)null).SetProperty(value => value.LastFailureCode, "delivery.failed"), cancellationToken);
                }
                finally { context.ChangeTracker.Clear(); }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { throw new InvalidOperationException("Background processing failed."); }
    }

    public async Task<OutboxMessage?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var message = await context.Set<OutboxMessage>().FromSqlInterpolated($"""
            SELECT TOP(1) * FROM [operations].[Outbox] WITH (UPDLOCK,READPAST,ROWLOCK)
            WHERE ([State]={(int)OutboxState.Pending} AND [NextAttemptAt]<={now})
               OR ([State]={(int)OutboxState.Processing} AND [LeaseUntil]<={now})
            ORDER BY [CreatedAt],[Id]
            """).SingleOrDefaultAsync(cancellationToken);
        if (message is null) return null;
        message.State = OutboxState.Processing; message.LeaseId = Guid.CreateVersion7(); message.LeaseUntil = now.AddMinutes(2); message.Attempts++;
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        context.Entry(message).State = EntityState.Detached;
        return message;
    }
}
