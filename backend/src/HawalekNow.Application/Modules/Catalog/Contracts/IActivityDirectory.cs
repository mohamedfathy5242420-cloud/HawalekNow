namespace HawalekNow.Application.Modules.Catalog.Contracts;

public interface IActivityDirectory
{
    // Caller owns the shared SQL transaction. The Catalog writer lock remains held until its Commit/Rollback.
    Task<bool> CanSelectInsideTransactionAsync(Guid activityId, CancellationToken cancellationToken);
}
