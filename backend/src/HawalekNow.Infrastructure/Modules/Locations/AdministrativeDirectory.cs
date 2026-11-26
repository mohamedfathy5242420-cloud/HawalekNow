using HawalekNow.Application.Modules.Locations.Contracts;
using HawalekNow.Domain.Modules.Locations;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Locations;

public sealed class AdministrativeDirectory(HawalekDbContext context, LocationStateLock stateLock) : IAdministrativeDirectory
{
    public async Task<bool> CanSelectInsideTransactionAsync(Guid governorateId, Guid cityId, Guid areaId, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Administrative selection requires the owning transaction.");
        await stateLock.AcquireAsync(true, cancellationToken);
        return await (from area in context.Set<Area>().AsNoTracking()
                      join city in context.Set<City>().AsNoTracking() on new { area.CityId, area.GovernorateId } equals new { CityId = city.Id, city.GovernorateId }
                      join governorate in context.Set<Governorate>().AsNoTracking() on city.GovernorateId equals governorate.Id
                      where area.Id == areaId && city.Id == cityId && governorate.Id == governorateId && area.IsActive && city.IsActive && governorate.IsActive
                      select area.Id).AnyAsync(cancellationToken);
    }
}
