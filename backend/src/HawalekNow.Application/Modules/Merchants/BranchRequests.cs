using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Merchants;
using MediatR;

namespace HawalekNow.Application.Modules.Merchants;

public sealed record BranchWrite(string Kind, string Name, string Address, Guid GovernorateId, Guid CityId, Guid AreaId,
    double? Latitude, double? Longitude, bool PublicFixedAddressConfirmed, string? Phone = null, string? WhatsApp = null);
public sealed record BranchUpdateWrite(string RowVersion, BranchWrite Details);
public sealed record BranchDto(Guid Id, Guid StoreId, string Kind, string Name, string Address, Guid GovernorateId, Guid CityId, Guid AreaId,
    double Latitude, double Longitude, string? Phone, string? WhatsApp, string? EffectivePhone, string? EffectiveWhatsApp, bool PublicFixedAddressConfirmed, string RowVersion);
public sealed record PublicBranchDto(Guid Id, Guid StoreId, string Kind, string Name, string Address, Guid GovernorateId, Guid CityId, Guid AreaId,
    double Latitude, double Longitude, string? Phone, string? WhatsApp);
public interface IBranchService
{
    Task<Result<BranchDto>> CreateAsync(Guid actorId, Guid storeId, BranchWrite value, CancellationToken cancellationToken);
    Task<Result<BranchDto>> UpdateAsync(Guid actorId, Guid storeId, Guid id, BranchUpdateWrite value, CancellationToken cancellationToken);
    Task<Result<MerchantPage<BranchDto>>> OwnListAsync(Guid actorId, Guid storeId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<MerchantPage<PublicBranchDto>>> PublicListAsync(Guid storeId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<PublicBranchDto>> PublicAsync(Guid id, CancellationToken cancellationToken);
}
public sealed record CreateBranchCommand(Guid ActorId, Guid StoreId, BranchWrite Value) : IRequest<Result<BranchDto>>;
public sealed record UpdateBranchCommand(Guid ActorId, Guid StoreId, Guid Id, BranchUpdateWrite Value) : IRequest<Result<BranchDto>>;
public sealed record GetOwnBranchesQuery(Guid ActorId, Guid StoreId, int Page = 1, int PageSize = 25) : IRequest<Result<MerchantPage<BranchDto>>>;
public sealed record GetPublicBranchesQuery(Guid StoreId, int Page = 1, int PageSize = 25) : IRequest<Result<MerchantPage<PublicBranchDto>>>;
public sealed record GetPublicBranchQuery(Guid Id) : IRequest<Result<PublicBranchDto>>;
public sealed class CreateBranchHandler(IBranchService service) : IRequestHandler<CreateBranchCommand, Result<BranchDto>>
{
    public Task<Result<BranchDto>> Handle(CreateBranchCommand request, CancellationToken cancellationToken) => service.CreateAsync(request.ActorId, request.StoreId, request.Value, cancellationToken);
}
public sealed class UpdateBranchHandler(IBranchService service) : IRequestHandler<UpdateBranchCommand, Result<BranchDto>>
{
    public Task<Result<BranchDto>> Handle(UpdateBranchCommand request, CancellationToken cancellationToken) => service.UpdateAsync(request.ActorId, request.StoreId, request.Id, request.Value, cancellationToken);
}
public sealed class GetOwnBranchesHandler(IBranchService service) : IRequestHandler<GetOwnBranchesQuery, Result<MerchantPage<BranchDto>>>
{
    public Task<Result<MerchantPage<BranchDto>>> Handle(GetOwnBranchesQuery request, CancellationToken cancellationToken) => service.OwnListAsync(request.ActorId, request.StoreId, request.Page, request.PageSize, cancellationToken);
}
public sealed class GetPublicBranchesHandler(IBranchService service) : IRequestHandler<GetPublicBranchesQuery, Result<MerchantPage<PublicBranchDto>>>
{
    public Task<Result<MerchantPage<PublicBranchDto>>> Handle(GetPublicBranchesQuery request, CancellationToken cancellationToken) => service.PublicListAsync(request.StoreId, request.Page, request.PageSize, cancellationToken);
}
public sealed class GetPublicBranchHandler(IBranchService service) : IRequestHandler<GetPublicBranchQuery, Result<PublicBranchDto>>
{
    public Task<Result<PublicBranchDto>> Handle(GetPublicBranchQuery request, CancellationToken cancellationToken) => service.PublicAsync(request.Id, cancellationToken);
}
public static class BranchInput
{
    public static bool Details(BranchWrite? value) => Normalize(value, out _);
    public static bool Normalize(BranchWrite? value, out BranchDetails? details)
    {
        details = null;
        return value is not null && BranchDetails.TryCreate(value.Kind, value.Name, value.Address, value.GovernorateId, value.CityId, value.AreaId,
            value.Latitude, value.Longitude, value.Phone, value.WhatsApp, value.PublicFixedAddressConfirmed, out details);
    }
}
public sealed class CreateBranchValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.StoreId).NotEmpty(); RuleFor(value => value.Value).Must(BranchInput.Details); }
}
public sealed class UpdateBranchValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchValidator()
    {
        RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.StoreId).NotEmpty(); RuleFor(value => value.Id).NotEmpty();
        RuleFor(value => value.Value).Must(value => value is not null && MerchantReviewInput.Version(value.RowVersion) && BranchInput.Details(value.Details));
    }
}
public sealed class GetOwnBranchesValidator : AbstractValidator<GetOwnBranchesQuery>
{
    public GetOwnBranchesValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.StoreId).NotEmpty(); RuleFor(value => value).Must(value => MerchantReviewInput.Page(value.Page, value.PageSize)); }
}
public sealed class GetPublicBranchesValidator : AbstractValidator<GetPublicBranchesQuery>
{
    public GetPublicBranchesValidator() { RuleFor(value => value.StoreId).NotEmpty(); RuleFor(value => value).Must(value => MerchantReviewInput.Page(value.Page, value.PageSize)); }
}
public sealed class GetPublicBranchValidator : AbstractValidator<GetPublicBranchQuery>
{
    public GetPublicBranchValidator() => RuleFor(value => value.Id).NotEmpty();
}
