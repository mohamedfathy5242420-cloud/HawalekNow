using HawalekNow.Infrastructure.Modules.Catalog;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Persistence;

namespace HawalekNow.IntegrationTests;

public static class ActivityChangeTestData
{
    public static ActivityChangeService Service(HawalekDbContext context, TimeProvider? clock = null)
    {
        var identity = new AccountAccess(context, new IdentityStateLock(context));
        return new(context, new MerchantStoreAccess(context, identity), identity, new ActivityDirectory(context, new CatalogTreeLock(context)), clock ?? TimeProvider.System);
    }
}
