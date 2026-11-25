using System.Collections.Immutable;
using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Locations;
using MediatR;

namespace HawalekNow.Application.Modules.Locations;

public enum LocationKind { Governorate = 1, City = 2, Area = 3 }
public sealed record LocationWrite(string NameAr, string NameEn, bool IsActive, Guid? GovernorateId = null, Guid? CityId = null, string? RowVersion = null);
public sealed record LocationDelete(string RowVersion);
public sealed record LocationAdminDto(Guid Id, string NameAr, string NameEn, bool IsActive, Guid? GovernorateId, Guid? CityId, string RowVersion);
public sealed record LocationOption(Guid Id, string Name);
public sealed record LocationPage<T>(ImmutableArray<T> Items, int Page, int PageSize, bool HasMore);
public interface ILocationAdministration
{
    Task<Result<LocationAdminDto>> SaveAsync(LocationKind kind, Guid? id, LocationWrite value, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(LocationKind kind, Guid id, string rowVersion, CancellationToken cancellationToken);
}
public interface ILocationQueries
{
    Task<Result<LocationPage<LocationOption>>> OptionsAsync(LocationKind kind, Guid? governorateId, Guid? cityId, string language, int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<LocationPage<LocationAdminDto>>> AdminAsync(LocationKind kind, Guid? governorateId, Guid? cityId, int page, int pageSize, CancellationToken cancellationToken);
}
public sealed record SaveLocationCommand(LocationKind Kind, Guid? Id, LocationWrite Value) : IRequest<Result<LocationAdminDto>>;
public sealed record DeleteLocationCommand(LocationKind Kind, Guid Id, string RowVersion) : IRequest<Result>;
public sealed record LocationOptionsQuery(LocationKind Kind, Guid? GovernorateId = null, Guid? CityId = null, string Language = "ar", int Page = 1, int PageSize = 30) : IRequest<Result<LocationPage<LocationOption>>>;
public sealed record AdminLocationsQuery(LocationKind Kind, Guid? GovernorateId = null, Guid? CityId = null, int Page = 1, int PageSize = 30) : IRequest<Result<LocationPage<LocationAdminDto>>>;
public sealed class SaveLocationHandler(ILocationAdministration service) : IRequestHandler<SaveLocationCommand, Result<LocationAdminDto>>
{
    public Task<Result<LocationAdminDto>> Handle(SaveLocationCommand request, CancellationToken cancellationToken) => service.SaveAsync(request.Kind, request.Id, request.Value, cancellationToken);
}
public sealed class DeleteLocationHandler(ILocationAdministration service) : IRequestHandler<DeleteLocationCommand, Result>
{
    public Task<Result> Handle(DeleteLocationCommand request, CancellationToken cancellationToken) => service.DeleteAsync(request.Kind, request.Id, request.RowVersion, cancellationToken);
}
public sealed class LocationOptionsHandler(ILocationQueries service) : IRequestHandler<LocationOptionsQuery, Result<LocationPage<LocationOption>>>
{
    public Task<Result<LocationPage<LocationOption>>> Handle(LocationOptionsQuery request, CancellationToken cancellationToken) => service.OptionsAsync(request.Kind, request.GovernorateId, request.CityId, request.Language, request.Page, request.PageSize, cancellationToken);
}
public sealed class AdminLocationsHandler(ILocationQueries service) : IRequestHandler<AdminLocationsQuery, Result<LocationPage<LocationAdminDto>>>
{
    public Task<Result<LocationPage<LocationAdminDto>>> Handle(AdminLocationsQuery request, CancellationToken cancellationToken) => service.AdminAsync(request.Kind, request.GovernorateId, request.CityId, request.Page, request.PageSize, cancellationToken);
}
public static class LocationInput
{
    public static bool Name(string value) { try { _ = LocationNames.Validate(value); return true; } catch (ArgumentException) { return false; } }
    public static bool Version(string? value) => value is not null && Convert.TryFromBase64String(value, new byte[8], out var size) && size == 8;
    public static bool Page(int page, int pageSize) => page is >= 1 and <= 100_000 && pageSize is >= 1 and <= 100;
    public static bool Parents(LocationKind kind, Guid? governorateId, Guid? cityId) => kind switch
    {
        LocationKind.Governorate => governorateId is null && cityId is null,
        LocationKind.City => governorateId is not null && governorateId != Guid.Empty && cityId is null,
        LocationKind.Area => governorateId is not null && governorateId != Guid.Empty && cityId is not null && cityId != Guid.Empty,
        _ => false
    };
}
public sealed class SaveLocationValidator : AbstractValidator<SaveLocationCommand>
{
    public SaveLocationValidator()
    {
        RuleFor(value => value.Kind).IsInEnum(); RuleFor(value => value.Id).Must(value => value != Guid.Empty);
        RuleFor(value => value.Value).NotNull();
        When(value => value.Value is not null, () =>
        {
            RuleFor(value => value.Value.NameAr).Must(LocationInput.Name); RuleFor(value => value.Value.NameEn).Must(LocationInput.Name);
            RuleFor(value => value).Must(value => LocationInput.Parents(value.Kind, value.Value.GovernorateId, value.Value.CityId));
            RuleFor(value => value.Value.RowVersion).Must(LocationInput.Version).When(value => value.Id is not null);
        });
    }
}
public sealed class DeleteLocationValidator : AbstractValidator<DeleteLocationCommand>
{
    public DeleteLocationValidator() { RuleFor(value => value.Kind).IsInEnum(); RuleFor(value => value.Id).NotEmpty(); RuleFor(value => value.RowVersion).Must(LocationInput.Version); }
}
