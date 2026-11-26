namespace HawalekNow.Application.Modules.Locations.Contracts;

public interface IAdministrativeDirectory
{
    // Shared hierarchy lock and the caller's UoW transaction protect the full active selection through Commit.
    Task<bool> CanSelectInsideTransactionAsync(Guid governorateId, Guid cityId, Guid areaId, CancellationToken cancellationToken);
}
