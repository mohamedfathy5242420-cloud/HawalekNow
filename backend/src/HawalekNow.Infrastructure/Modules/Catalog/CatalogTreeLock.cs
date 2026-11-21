using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Catalog;

public sealed class CatalogTreeLock(HawalekDbContext context)
{
    // All writes, including delete and activation, share this transaction-owned lock.
    // Lock before reading ancestry: row tokens alone cannot prevent cross-row cycles.
    public Task AcquireAsync(CancellationToken cancellationToken) => context.Database.ExecuteSqlInterpolatedAsync($"""
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock @Resource={"catalog-reference-tree"}, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
        IF @result < 0 THROW 51010, N'Reference tree lock unavailable.', 1;
        """, cancellationToken);
}
