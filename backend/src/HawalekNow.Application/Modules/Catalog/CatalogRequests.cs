using System.Collections.Immutable;
using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Catalog;
using MediatR;

namespace HawalekNow.Application.Modules.Catalog;

public sealed record ActivityWrite(string NameAr, string NameEn, bool IsActive, string? RowVersion = null);
public sealed record CategoryWrite(Guid ActivityId, Guid? ParentId, string NameAr, string NameEn, bool IsActive, string? RowVersion = null);
public sealed record DeleteReference(string RowVersion);
public sealed record ActivityAdminDto(Guid Id, string NameAr, string NameEn, bool IsActive, string RowVersion);
public sealed record CategoryAdminDto(Guid Id, Guid ActivityId, Guid? ParentId, string NameAr, string NameEn, bool IsActive, string RowVersion);
public sealed record CatalogOption(Guid Id, string Name);
public sealed record CatalogPage<T>(ImmutableArray<T> Items, int Page, int PageSize, bool HasMore);

public interface ICatalogAdministration
{
    Task<Result<ActivityAdminDto>> SaveActivityAsync(Guid? id, ActivityWrite value, CancellationToken cancellationToken);
    Task<Result<CategoryAdminDto>> SaveCategoryAsync(Guid? id, CategoryWrite value, CancellationToken cancellationToken);
    Task<Result> DeleteActivityAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
    Task<Result> DeleteCategoryAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
}
public interface ICatalogQueries
{
    Task<Result<CatalogPage<CatalogOption>>> ActivitiesAsync(string language, int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<CatalogPage<CatalogOption>>> CategoriesAsync(Guid activityId, Guid? parentId, string language, int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<CatalogPage<ActivityAdminDto>>> AdminActivitiesAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<CatalogPage<CategoryAdminDto>>> AdminCategoriesAsync(Guid activityId, Guid? parentId, int page, int pageSize, CancellationToken cancellationToken);
}
public sealed record SaveActivityCommand(Guid? Id, ActivityWrite Value) : IRequest<Result<ActivityAdminDto>>;
public sealed record SaveCategoryCommand(Guid? Id, CategoryWrite Value) : IRequest<Result<CategoryAdminDto>>;
public sealed record DeleteActivityCommand(Guid Id, string RowVersion) : IRequest<Result>;
public sealed record DeleteCategoryCommand(Guid Id, string RowVersion) : IRequest<Result>;
public sealed record ListActivitiesQuery(string Language = "ar", int Page = 1, int PageSize = 30) : IRequest<Result<CatalogPage<CatalogOption>>>;
public sealed record ListCategoriesQuery(Guid ActivityId, Guid? ParentId, string Language = "ar", int Page = 1, int PageSize = 30) : IRequest<Result<CatalogPage<CatalogOption>>>;
public sealed record ListAdminActivitiesQuery(int Page = 1, int PageSize = 30) : IRequest<Result<CatalogPage<ActivityAdminDto>>>;
public sealed record ListAdminCategoriesQuery(Guid ActivityId, Guid? ParentId, int Page = 1, int PageSize = 30) : IRequest<Result<CatalogPage<CategoryAdminDto>>>;

public sealed class SaveActivityHandler(ICatalogAdministration service) : IRequestHandler<SaveActivityCommand, Result<ActivityAdminDto>>
{
    public Task<Result<ActivityAdminDto>> Handle(SaveActivityCommand request, CancellationToken cancellationToken) => service.SaveActivityAsync(request.Id, request.Value, cancellationToken);
}
public sealed class SaveCategoryHandler(ICatalogAdministration service) : IRequestHandler<SaveCategoryCommand, Result<CategoryAdminDto>>
{
    public Task<Result<CategoryAdminDto>> Handle(SaveCategoryCommand request, CancellationToken cancellationToken) => service.SaveCategoryAsync(request.Id, request.Value, cancellationToken);
}
public sealed class DeleteActivityHandler(ICatalogAdministration service) : IRequestHandler<DeleteActivityCommand, Result>
{
    public Task<Result> Handle(DeleteActivityCommand request, CancellationToken cancellationToken) => service.DeleteActivityAsync(request.Id, request.RowVersion, cancellationToken);
}
public sealed class DeleteCategoryHandler(ICatalogAdministration service) : IRequestHandler<DeleteCategoryCommand, Result>
{
    public Task<Result> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken) => service.DeleteCategoryAsync(request.Id, request.RowVersion, cancellationToken);
}
public sealed class ListActivitiesHandler(ICatalogQueries service) : IRequestHandler<ListActivitiesQuery, Result<CatalogPage<CatalogOption>>>
{
    public Task<Result<CatalogPage<CatalogOption>>> Handle(ListActivitiesQuery request, CancellationToken cancellationToken) => service.ActivitiesAsync(request.Language, request.Page, request.PageSize, cancellationToken);
}
public sealed class ListCategoriesHandler(ICatalogQueries service) : IRequestHandler<ListCategoriesQuery, Result<CatalogPage<CatalogOption>>>
{
    public Task<Result<CatalogPage<CatalogOption>>> Handle(ListCategoriesQuery request, CancellationToken cancellationToken) => service.CategoriesAsync(request.ActivityId, request.ParentId, request.Language, request.Page, request.PageSize, cancellationToken);
}
public sealed class ListAdminActivitiesHandler(ICatalogQueries service) : IRequestHandler<ListAdminActivitiesQuery, Result<CatalogPage<ActivityAdminDto>>>
{
    public Task<Result<CatalogPage<ActivityAdminDto>>> Handle(ListAdminActivitiesQuery request, CancellationToken cancellationToken) => service.AdminActivitiesAsync(request.Page, request.PageSize, cancellationToken);
}
public sealed class ListAdminCategoriesHandler(ICatalogQueries service) : IRequestHandler<ListAdminCategoriesQuery, Result<CatalogPage<CategoryAdminDto>>>
{
    public Task<Result<CatalogPage<CategoryAdminDto>>> Handle(ListAdminCategoriesQuery request, CancellationToken cancellationToken) => service.AdminCategoriesAsync(request.ActivityId, request.ParentId, request.Page, request.PageSize, cancellationToken);
}

public static class CatalogInput
{
    public static bool Name(string value) { try { _ = CatalogNames.Key(value); return true; } catch (ArgumentException) { return false; } }
    public static bool Version(string? value) => value is not null && Convert.TryFromBase64String(value, new byte[8], out var size) && size == 8;
    public static bool Page(int page, int pageSize) => page is >= 1 and <= 100_000 && pageSize is >= 1 and <= 100;
    public static bool Language(string value) => value is "ar" or "en";
}
public sealed class SaveActivityValidator : AbstractValidator<SaveActivityCommand>
{
    public SaveActivityValidator()
    {
        RuleFor(value => value.Id).Must(value => value != Guid.Empty);
        RuleFor(value => value.Value).NotNull();
        When(value => value.Value is not null, () =>
        {
            RuleFor(value => value.Value.NameAr).Must(CatalogInput.Name);
            RuleFor(value => value.Value.NameEn).Must(CatalogInput.Name);
            RuleFor(value => value.Value.RowVersion).Must(CatalogInput.Version).When(value => value.Id is not null);
        });
    }
}
public sealed class SaveCategoryValidator : AbstractValidator<SaveCategoryCommand>
{
    public SaveCategoryValidator()
    {
        RuleFor(value => value.Id).Must(value => value != Guid.Empty);
        RuleFor(value => value.Value).NotNull();
        When(value => value.Value is not null, () =>
        {
            RuleFor(value => value.Value.ActivityId).NotEmpty();
            RuleFor(value => value.Value.ParentId).Must(value => value != Guid.Empty);
            RuleFor(value => value.Value.NameAr).Must(CatalogInput.Name);
            RuleFor(value => value.Value.NameEn).Must(CatalogInput.Name);
            RuleFor(value => value.Value.RowVersion).Must(CatalogInput.Version).When(value => value.Id is not null);
        });
    }
}
public sealed class DeleteActivityValidator : AbstractValidator<DeleteActivityCommand>
{
    public DeleteActivityValidator() { RuleFor(value => value.Id).NotEmpty(); RuleFor(value => value.RowVersion).Must(CatalogInput.Version); }
}
public sealed class DeleteCategoryValidator : AbstractValidator<DeleteCategoryCommand>
{
    public DeleteCategoryValidator() { RuleFor(value => value.Id).NotEmpty(); RuleFor(value => value.RowVersion).Must(CatalogInput.Version); }
}
