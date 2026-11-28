using HawalekNow.Application.Shared.Storage;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Shared.Storage;

public sealed class StoredFileLock(HawalekDbContext context)
{
    public Task AcquireAsync(string key, CancellationToken cancellationToken)
    {
        if (!ImageStorageLimits.ValidKey(key) || context.Database.CurrentTransaction is null) throw new InvalidOperationException("A stored file lock requires an owned transaction and generated key.");
        return context.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource={"stored-image:" + key}, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51021, N'Stored image lock unavailable.', 1;
            """, cancellationToken);
    }
}
