using HawalekNow.Application.Shared.Storage;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Locations;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Persistence;

namespace HawalekNow.IntegrationTests;

public static class StoreAdministrationTestData
{
    public static StoreAdministration Service(SqlDatabase database, HawalekDbContext context, IFileStorage files, TimeProvider? clock = null)
    {
        var time = clock ?? TimeProvider.System; var identity = new AccountAccess(context, new IdentityStateLock(context)); var access = new MerchantStoreAccess(context, identity);
        return new(context, access, identity, StoreTestData.Service(database, context, files, time),
            new(context, access, new AdministrativeDirectory(context, new LocationStateLock(context)), time), time);
    }
}
