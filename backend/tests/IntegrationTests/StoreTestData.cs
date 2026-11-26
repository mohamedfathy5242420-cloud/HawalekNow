using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Storage;
using HawalekNow.Domain.Modules.Catalog;
using HawalekNow.Infrastructure.Modules.Catalog;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Persistence;
using HawalekNow.Infrastructure.Shared.Storage;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.IntegrationTests;

public static class StoreTestData
{
    public static string Root() => Path.Combine(Path.GetTempPath(), "HN_StoreTest_" + Guid.NewGuid().ToString("N"), "images");
    public static StoreWrite Details(string name = "متجر الاختبار") => new(name, "+201012345678", null, "https://facebook.com/test-store");
    public static async Task<Guid> ActivityAsync(SqlDatabase database, bool active = true)
    {
        await using var context = database.CreateContext(); var key = Guid.NewGuid().ToString("N");
        var activity = Activity.Create("نشاط " + key, "Activity " + key, active); context.Add(activity); await context.SaveChangesAsync(); return activity.Id;
    }
    public static StoreService Service(SqlDatabase database, HawalekDbContext context, IFileStorage files, TimeProvider? time = null)
    {
        var clock = time ?? TimeProvider.System; var fileLock = new StoredFileLock(context);
        return new(context, new MerchantStoreAccess(context, new AccountAccess(context, new IdentityStateLock(context))),
            new ActivityDirectory(context, new CatalogTreeLock(context)), files, fileLock,
            new LogoDeletionIntents(new OutboxWriter(context, clock), new PayloadProtection(TestIdentityConfiguration.Create(database.ConnectionString, database.IdentifierKey))),
            new LogoCleanup(context, files, fileLock), clock);
    }
    public static async Task<StoreDto> CreateAsync(SqlDatabase database, MerchantTestAccount owner, IFileStorage files, Guid? activity = null)
    {
        var activityId = activity ?? await ActivityAsync(database);
        await using var context = database.CreateContext(); var result = await Service(database, context, files).CreateAsync(owner.Id, new(activityId, Details()), default);
        Assert.True(result.IsSuccess); return result.Value;
    }
}
