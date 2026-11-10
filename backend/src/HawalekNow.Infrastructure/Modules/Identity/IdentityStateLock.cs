using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class IdentityStateLock(HawalekDbContext context)
{
    public Task AcquireAsync(Guid userId, CancellationToken cancellationToken) => context.Database.ExecuteSqlInterpolatedAsync($"""
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock @Resource={"identity-state:" + userId.ToString("N")}, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
        IF @result < 0 THROW 51003, N'Account state lock unavailable.', 1;
        """, cancellationToken);
}
