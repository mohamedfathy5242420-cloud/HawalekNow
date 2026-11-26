using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Locations;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Locations;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Persistence;

namespace HawalekNow.IntegrationTests;

public sealed record AdministrativePath(Guid Governorate, Guid City, Guid Area);
public static class BranchTestData
{
    public static async Task<AdministrativePath> PathAsync(SqlDatabase database, bool active = true)
    {
        await using var context = database.CreateContext(); var suffix = Guid.NewGuid().ToString("N");
        var governorate = Governorate.Create("محافظة " + suffix, "Governorate " + suffix);
        var city = City.Create(governorate.Id, "مدينة " + suffix, "City " + suffix);
        var area = Area.Create(governorate.Id, city.Id, "منطقة " + suffix, "Area " + suffix, active);
        context.AddRange(governorate, city, area); await context.SaveChangesAsync(); return new(governorate.Id, city.Id, area.Id);
    }
    public static BranchWrite Write(AdministrativePath path, string kind = "Branch", string? phone = null, string? whatsApp = null) =>
        new(kind, "مكان تجاري", "العنوان العام الثابت", path.Governorate, path.City, path.Area, 30.0444, 31.2357, true, phone, whatsApp);
    public static BranchService Service(HawalekDbContext context) => new(context,
        new MerchantStoreAccess(context, new AccountAccess(context, new IdentityStateLock(context))),
        new AdministrativeDirectory(context, new LocationStateLock(context)), TimeProvider.System);
}
