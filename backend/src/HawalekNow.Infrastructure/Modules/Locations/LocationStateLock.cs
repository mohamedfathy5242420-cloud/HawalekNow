using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Locations;

public sealed class LocationStateLock(HawalekDbContext context)
{
    public Task AcquireAsync(bool readOnly, CancellationToken cancellationToken, int timeoutMilliseconds = 10000) => context.Database.ExecuteSqlInterpolatedAsync($"""
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock @Resource={"locations-reference-hierarchy"}, @LockMode={(readOnly ? "Shared" : "Exclusive")}, @LockOwner=N'Transaction', @LockTimeout={timeoutMilliseconds};
        IF @result < 0 THROW 51011, N'Reference hierarchy lock unavailable.', 1;
        """, cancellationToken);
}
