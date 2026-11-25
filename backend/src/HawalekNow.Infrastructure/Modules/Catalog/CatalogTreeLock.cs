using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Catalog;

public sealed class CatalogTreeLock(HawalekDbContext context)
{
    // All writes, including delete and activation, share this transaction-owned lock.
    // Lock before reading ancestry: row tokens alone cannot prevent cross-row cycles.
    public async Task AcquireAsync(CancellationToken cancellationToken, int timeoutMilliseconds = 10000)
    {
        var previous = context.Database.GetCommandTimeout();
        // The explicit long seed lock must not be cut short by SqlCommand's default30s.
        var extend = timeoutMilliseconds > 20000 && previous != 0;
        if (extend) context.Database.SetCommandTimeout(Math.Max(previous ?? 30, checked((timeoutMilliseconds + 999) / 1000 + 5)));
        try
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock @Resource={"catalog-reference-tree"}, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout={timeoutMilliseconds};
        IF @result < 0 THROW 51010, N'Reference tree lock unavailable.', 1;
        """, cancellationToken);
        }
        finally { if (extend) context.Database.SetCommandTimeout(previous); }
    }
}
