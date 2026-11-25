using System.Collections.Immutable;
using HawalekNow.Application.Modules.Locations;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Locations;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Locations;

public sealed class LocationQueries(HawalekDbContext context, LocationStateLock stateLock) : ILocationQueries
{
    public async Task<Result<LocationPage<LocationOption>>> OptionsAsync(LocationKind kind, Guid? governorateId, Guid? cityId, string language, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!LocationInput.Parents(kind, governorateId, cityId) || !LocationInput.Page(page, pageSize) || language is not ("ar" or "en")) return Invalid<LocationOption>();
        await using var transaction = await context.BeginAsync(cancellationToken); await stateLock.AcquireAsync(true, cancellationToken);
        if (kind != LocationKind.Governorate && !await context.Set<Governorate>().AnyAsync(value => value.Id == governorateId && value.IsActive, cancellationToken)) return Page(Array.Empty<LocationOption>(), page, pageSize);
        if (kind == LocationKind.Area && !await context.Set<City>().AnyAsync(value => value.Id == cityId && value.GovernorateId == governorateId && value.IsActive, cancellationToken)) return Page(Array.Empty<LocationOption>(), page, pageSize);
        var rows = kind switch
        {
            LocationKind.Governorate => await context.Set<Governorate>().AsNoTracking().Where(value => value.IsActive).OrderBy(value => value.Id)
                .Skip((page - 1) * pageSize).Take(pageSize + 1).Select(value => new LocationOption(value.Id, language == "ar" ? value.NameAr : value.NameEn)).ToArrayAsync(cancellationToken),
            LocationKind.City => await context.Set<City>().AsNoTracking().Where(value => value.GovernorateId == governorateId && value.IsActive).OrderBy(value => value.Id)
                .Skip((page - 1) * pageSize).Take(pageSize + 1).Select(value => new LocationOption(value.Id, language == "ar" ? value.NameAr : value.NameEn)).ToArrayAsync(cancellationToken),
            LocationKind.Area => await context.Set<Area>().AsNoTracking().Where(value => value.GovernorateId == governorateId && value.CityId == cityId && value.IsActive).OrderBy(value => value.Id)
                .Skip((page - 1) * pageSize).Take(pageSize + 1).Select(value => new LocationOption(value.Id, language == "ar" ? value.NameAr : value.NameEn)).ToArrayAsync(cancellationToken),
            _ => []
        };
        await transaction.CommitAsync(cancellationToken); return Page(rows, page, pageSize);
    }
    public async Task<Result<LocationPage<LocationAdminDto>>> AdminAsync(LocationKind kind, Guid? governorateId, Guid? cityId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!LocationInput.Parents(kind, governorateId, cityId) || !LocationInput.Page(page, pageSize)) return Invalid<LocationAdminDto>();
        LocationAdminDto[] rows;
        switch (kind)
        {
            case LocationKind.Governorate:
                var governorates = await context.Set<Governorate>().AsNoTracking().OrderBy(value => value.Id).Skip((page - 1) * pageSize).Take(pageSize + 1).ToArrayAsync(cancellationToken);
                rows = governorates.Select(LocationAdministration.ToDto).ToArray(); break;
            case LocationKind.City:
                var cities = await context.Set<City>().AsNoTracking().Where(value => value.GovernorateId == governorateId).OrderBy(value => value.Id).Skip((page - 1) * pageSize).Take(pageSize + 1).ToArrayAsync(cancellationToken);
                rows = cities.Select(LocationAdministration.ToDto).ToArray(); break;
            case LocationKind.Area:
                var areas = await context.Set<Area>().AsNoTracking().Where(value => value.GovernorateId == governorateId && value.CityId == cityId).OrderBy(value => value.Id).Skip((page - 1) * pageSize).Take(pageSize + 1).ToArrayAsync(cancellationToken);
                rows = areas.Select(LocationAdministration.ToDto).ToArray(); break;
            default: return Invalid<LocationAdminDto>();
        }
        return Page(rows, page, pageSize);
    }
    private static Result<LocationPage<T>> Invalid<T>() => Result<LocationPage<T>>.Failure(new(ErrorKind.Validation));
    private static Result<LocationPage<T>> Page<T>(T[] rows, int page, int pageSize) => Result<LocationPage<T>>.Success(new(rows.Take(pageSize).ToImmutableArray(), page, pageSize, rows.Length > pageSize));
}
