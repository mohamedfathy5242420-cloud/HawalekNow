using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Locations;

public sealed class LocationStateLock(HawalekDbContext context)
{
    public async Task AcquireAsync(bool readOnly, CancellationToken cancellationToken, int timeoutMilliseconds = 10000)
    {
        var previous = context.Database.GetCommandTimeout();
        var extend = timeoutMilliseconds > 20000 && previous != 0;
        if (extend) context.Database.SetCommandTimeout(Math.Max(previous ?? 30, checked((timeoutMilliseconds + 999) / 1000 + 5)));
        try
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"""
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock @Resource={"locations-reference-hierarchy"}, @LockMode={(readOnly ? "Shared" : "Exclusive")}, @LockOwner=N'Transaction', @LockTimeout={timeoutMilliseconds};
        IF @result < 0 THROW 51011, N'Reference hierarchy lock unavailable.', 1;
        """, cancellationToken);
        }
        finally { if (extend) context.Database.SetCommandTimeout(previous); }
    }
}
