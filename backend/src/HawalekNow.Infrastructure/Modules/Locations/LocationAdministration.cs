using HawalekNow.Application.Modules.Locations;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Locations;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Locations;

public sealed class LocationAdministration(HawalekDbContext context, LocationStateLock stateLock) : ILocationAdministration
{
    public async Task<Result<LocationAdminDto>> SaveAsync(LocationKind kind, Guid? id, LocationWrite value, CancellationToken cancellationToken)
    {
        if (value is null || id == Guid.Empty || !LocationInput.Name(value.NameAr) || !LocationInput.Name(value.NameEn) || !LocationInput.Parents(kind, value.GovernorateId, value.CityId) || (id is not null && !LocationInput.Version(value.RowVersion))) return Failure(ErrorKind.Validation);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken); await stateLock.AcquireAsync(false, cancellationToken);
            if (kind != LocationKind.Governorate && !await context.Set<Governorate>().AnyAsync(item => item.Id == value.GovernorateId, cancellationToken)) return Failure(ErrorKind.Validation);
            if (kind == LocationKind.Area && !await context.Set<City>().AnyAsync(item => item.Id == value.CityId && item.GovernorateId == value.GovernorateId, cancellationToken)) return Failure(ErrorKind.Validation);
            Func<LocationAdminDto> toDto;
            switch (kind)
            {
                case LocationKind.Governorate:
                    var governorate = id is null ? Governorate.Create(value.NameAr, value.NameEn, value.IsActive) : await context.Set<Governorate>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                    if (governorate is null) return Failure(ErrorKind.NotFound);
                    if (id is null) context.Add(governorate);
                    else { await context.Entry(governorate).ReloadAsync(cancellationToken); if (!Matches(governorate.RowVersion, value.RowVersion)) return Failure(ErrorKind.Conflict); governorate.Edit(value.NameAr, value.NameEn, value.IsActive); }
                    toDto = () => ToDto(governorate); break;
                case LocationKind.City:
                    var city = id is null ? City.Create(value.GovernorateId!.Value, value.NameAr, value.NameEn, value.IsActive) : await context.Set<City>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                    if (city is null) return Failure(ErrorKind.NotFound);
                    if (id is null) context.Add(city);
                    else
                    {
                        await context.Entry(city).ReloadAsync(cancellationToken); if (!Matches(city.RowVersion, value.RowVersion)) return Failure(ErrorKind.Conflict);
                        if (city.GovernorateId != value.GovernorateId)
                        {
                            if (await context.Set<Area>().AnyAsync(item => item.CityId == city.Id, cancellationToken)) return Failure(ErrorKind.Conflict);
                            // GovernorateId participates in the composite principal key. EF cannot mutate a tracked key;
                            // update it explicitly in the same locked transaction, preserving all other columns.
                            var expected = Convert.FromBase64String(value.RowVersion!);
                            var ar = LocationNames.Validate(value.NameAr); var en = LocationNames.Validate(value.NameEn);
                            var affected = await context.Set<City>().Where(item => item.Id == city.Id && item.RowVersion == expected)
                                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.GovernorateId, value.GovernorateId!.Value)
                                    .SetProperty(item => item.NameAr, ar).SetProperty(item => item.NameEn, en).SetProperty(item => item.IsActive, value.IsActive), cancellationToken);
                            if (affected != 1) return Failure(ErrorKind.Conflict);
                            context.Entry(city).State = EntityState.Detached;
                            city = await context.Set<City>().SingleAsync(item => item.Id == id, cancellationToken);
                        }
                        else city.Edit(value.GovernorateId!.Value, value.NameAr, value.NameEn, value.IsActive);
                    }
                    toDto = () => ToDto(city); break;
                case LocationKind.Area:
                    var area = id is null ? Area.Create(value.GovernorateId!.Value, value.CityId!.Value, value.NameAr, value.NameEn, value.IsActive) : await context.Set<Area>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                    if (area is null) return Failure(ErrorKind.NotFound);
                    if (id is null) context.Add(area);
                    else { await context.Entry(area).ReloadAsync(cancellationToken); if (!Matches(area.RowVersion, value.RowVersion)) return Failure(ErrorKind.Conflict); area.Edit(value.GovernorateId!.Value, value.CityId!.Value, value.NameAr, value.NameEn, value.IsActive); }
                    toDto = () => ToDto(area); break;
                default: return Failure(ErrorKind.Validation);
            }
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result<LocationAdminDto>.Success(toDto());
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Failure(ErrorKind.Conflict); }
        catch (SqlException error) when (error.Number is 1205 or 1222 or 51011) { context.ChangeTracker.Clear(); return Failure(ErrorKind.Conflict); }
    }
    public async Task<Result> DeleteAsync(LocationKind kind, Guid id, string rowVersion, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(kind) || id == Guid.Empty || !LocationInput.Version(rowVersion)) return Result.Failure(new(ErrorKind.Validation));
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken); await stateLock.AcquireAsync(false, cancellationToken);
            switch (kind)
            {
                case LocationKind.Governorate:
                    var governorate = await context.Set<Governorate>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                    if (governorate is null) return Result.Failure(new(ErrorKind.NotFound)); await context.Entry(governorate).ReloadAsync(cancellationToken);
                    if (!Matches(governorate.RowVersion, rowVersion) || await context.Set<City>().AnyAsync(item => item.GovernorateId == id, cancellationToken)) return Result.Failure(new(ErrorKind.Conflict));
                    context.Remove(governorate); break;
                case LocationKind.City:
                    var city = await context.Set<City>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                    if (city is null) return Result.Failure(new(ErrorKind.NotFound)); await context.Entry(city).ReloadAsync(cancellationToken);
                    if (!Matches(city.RowVersion, rowVersion) || await context.Set<Area>().AnyAsync(item => item.CityId == id, cancellationToken)) return Result.Failure(new(ErrorKind.Conflict));
                    context.Remove(city); break;
                case LocationKind.Area:
                    var area = await context.Set<Area>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                    if (area is null) return Result.Failure(new(ErrorKind.NotFound)); await context.Entry(area).ReloadAsync(cancellationToken);
                    if (!Matches(area.RowVersion, rowVersion)) return Result.Failure(new(ErrorKind.Conflict)); context.Remove(area); break;
            }
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result.Success();
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Result.Failure(new(ErrorKind.Conflict)); }
        catch (SqlException error) when (error.Number is 1205 or 1222 or 51011) { context.ChangeTracker.Clear(); return Result.Failure(new(ErrorKind.Conflict)); }
    }
    private static bool Matches(byte[] actual, string? version) => version is not null && actual.AsSpan().SequenceEqual(Convert.FromBase64String(version));
    private static Result<LocationAdminDto> Failure(ErrorKind kind) => Result<LocationAdminDto>.Failure(new(kind));
    internal static LocationAdminDto ToDto(Governorate value) => new(value.Id, value.NameAr, value.NameEn, value.IsActive, null, null, Convert.ToBase64String(value.RowVersion));
    internal static LocationAdminDto ToDto(City value) => new(value.Id, value.NameAr, value.NameEn, value.IsActive, value.GovernorateId, null, Convert.ToBase64String(value.RowVersion));
    internal static LocationAdminDto ToDto(Area value) => new(value.Id, value.NameAr, value.NameEn, value.IsActive, value.GovernorateId, value.CityId, Convert.ToBase64String(value.RowVersion));
}
