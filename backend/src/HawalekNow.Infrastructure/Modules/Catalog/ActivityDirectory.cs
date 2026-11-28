using HawalekNow.Application.Modules.Catalog.Contracts;
using HawalekNow.Domain.Modules.Catalog;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Catalog;

public sealed class ActivityDirectory(HawalekDbContext context, CatalogTreeLock treeLock) : IActivityDirectory
{
    public async Task<bool> CanSelectInsideTransactionAsync(Guid activityId, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Activity selection requires the owning transaction.");
        await treeLock.AcquireAsync(cancellationToken);
        return await context.Set<Activity>().AsNoTracking().AnyAsync(value => value.Id == activityId && value.IsActive, cancellationToken);
    }
}
