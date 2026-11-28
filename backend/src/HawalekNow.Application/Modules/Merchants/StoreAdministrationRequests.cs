using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Merchants;
using MediatR;

namespace HawalekNow.Application.Modules.Merchants;

public enum StoreAdministrationAction { Hide = 1, Publish = 2, Suspend = 3, Resume = 4 }
public sealed record AdminStoreDto(StoreDto Store, Guid OwnerUserId, string MerchantStatus, bool OwnerActive, bool EmailConfirmed);
public sealed record AdminStoreUpdateWrite(string RowVersion, StoreWrite Details, string Reason);
public sealed record StoreModerationWrite(string RowVersion, string Reason);
public sealed record AdminBranchUpdateWrite(string RowVersion, BranchWrite Details, string Reason);
public interface IStoreAdministration
{
    Task<Result<AdminStoreDto>> ReadAsync(Guid actorId, Guid id, CancellationToken cancellationToken);
    Task<Result<MerchantPage<AdminStoreDto>>> ListAsync(Guid actorId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<StoreDto>> UpdateAsync(Guid actorId, Guid id, AdminStoreUpdateWrite value, CancellationToken cancellationToken);
    Task<Result<StoreDto>> ModerateAsync(Guid actorId, Guid id, StoreAdministrationAction action, StoreModerationWrite value, CancellationToken cancellationToken);
    Task<Result<StoreDto>> ChangeLogoAsync(Guid actorId, Guid id, string rowVersion, Stream? content, string reason, CancellationToken cancellationToken);
    Task<Result<byte[]>> ReadLogoAsync(Guid actorId, Guid id, CancellationToken cancellationToken);
    Task<Result<MerchantPage<BranchDto>>> BranchesAsync(Guid actorId, Guid storeId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<BranchDto>> UpdateBranchAsync(Guid actorId, Guid storeId, Guid id, AdminBranchUpdateWrite value, CancellationToken cancellationToken);
    Task<Result<MerchantPage<MerchantAuditDto>>> AuditAsync(Guid actorId, Guid id, bool branch, int page, int pageSize, CancellationToken cancellationToken);
}
public sealed record ReadAdminStoreQuery(Guid ActorId, Guid Id) : IRequest<Result<AdminStoreDto>>;
public sealed record ListAdminStoresQuery(Guid ActorId, int Page = 1, int PageSize = 25) : IRequest<Result<MerchantPage<AdminStoreDto>>>;
public sealed record UpdateAdminStoreCommand(Guid ActorId, Guid Id, AdminStoreUpdateWrite Value) : IRequest<Result<StoreDto>>;
public sealed record ModerateStoreCommand(Guid ActorId, Guid Id, StoreAdministrationAction Action, StoreModerationWrite Value) : IRequest<Result<StoreDto>>;
public sealed record ChangeAdminStoreLogoCommand(Guid ActorId, Guid Id, string RowVersion, Stream? Content, string Reason) : IRequest<Result<StoreDto>>;
public sealed record ReadAdminStoreLogoQuery(Guid ActorId, Guid Id) : IRequest<Result<byte[]>>;
public sealed record ListAdminBranchesQuery(Guid ActorId, Guid StoreId, int Page = 1, int PageSize = 25) : IRequest<Result<MerchantPage<BranchDto>>>;
public sealed record UpdateAdminBranchCommand(Guid ActorId, Guid StoreId, Guid Id, AdminBranchUpdateWrite Value) : IRequest<Result<BranchDto>>;
public sealed record ReadStoreAuditQuery(Guid ActorId, Guid Id, bool Branch = false, int Page = 1, int PageSize = 25) : IRequest<Result<MerchantPage<MerchantAuditDto>>>;
public sealed class ReadAdminStoreHandler(IStoreAdministration service) : IRequestHandler<ReadAdminStoreQuery, Result<AdminStoreDto>>
{
    public Task<Result<AdminStoreDto>> Handle(ReadAdminStoreQuery request, CancellationToken cancellationToken) => service.ReadAsync(request.ActorId, request.Id, cancellationToken);
}
public sealed class ListAdminStoresHandler(IStoreAdministration service) : IRequestHandler<ListAdminStoresQuery, Result<MerchantPage<AdminStoreDto>>>
{
    public Task<Result<MerchantPage<AdminStoreDto>>> Handle(ListAdminStoresQuery request, CancellationToken cancellationToken) => service.ListAsync(request.ActorId, request.Page, request.PageSize, cancellationToken);
}
public sealed class UpdateAdminStoreHandler(IStoreAdministration service) : IRequestHandler<UpdateAdminStoreCommand, Result<StoreDto>>
{
    public Task<Result<StoreDto>> Handle(UpdateAdminStoreCommand request, CancellationToken cancellationToken) => service.UpdateAsync(request.ActorId, request.Id, request.Value, cancellationToken);
}
public sealed class ModerateStoreHandler(IStoreAdministration service) : IRequestHandler<ModerateStoreCommand, Result<StoreDto>>
{
    public Task<Result<StoreDto>> Handle(ModerateStoreCommand request, CancellationToken cancellationToken) => service.ModerateAsync(request.ActorId, request.Id, request.Action, request.Value, cancellationToken);
}
public sealed class ChangeAdminStoreLogoHandler(IStoreAdministration service) : IRequestHandler<ChangeAdminStoreLogoCommand, Result<StoreDto>>
{
    public Task<Result<StoreDto>> Handle(ChangeAdminStoreLogoCommand request, CancellationToken cancellationToken) => service.ChangeLogoAsync(request.ActorId, request.Id, request.RowVersion, request.Content, request.Reason, cancellationToken);
}
public sealed class ReadAdminStoreLogoHandler(IStoreAdministration service) : IRequestHandler<ReadAdminStoreLogoQuery, Result<byte[]>>
{
    public Task<Result<byte[]>> Handle(ReadAdminStoreLogoQuery request, CancellationToken cancellationToken) => service.ReadLogoAsync(request.ActorId, request.Id, cancellationToken);
}
public sealed class ListAdminBranchesHandler(IStoreAdministration service) : IRequestHandler<ListAdminBranchesQuery, Result<MerchantPage<BranchDto>>>
{
    public Task<Result<MerchantPage<BranchDto>>> Handle(ListAdminBranchesQuery request, CancellationToken cancellationToken) => service.BranchesAsync(request.ActorId, request.StoreId, request.Page, request.PageSize, cancellationToken);
}
public sealed class UpdateAdminBranchHandler(IStoreAdministration service) : IRequestHandler<UpdateAdminBranchCommand, Result<BranchDto>>
{
    public Task<Result<BranchDto>> Handle(UpdateAdminBranchCommand request, CancellationToken cancellationToken) => service.UpdateBranchAsync(request.ActorId, request.StoreId, request.Id, request.Value, cancellationToken);
}
public sealed class ReadStoreAuditHandler(IStoreAdministration service) : IRequestHandler<ReadStoreAuditQuery, Result<MerchantPage<MerchantAuditDto>>>
{
    public Task<Result<MerchantPage<MerchantAuditDto>>> Handle(ReadStoreAuditQuery request, CancellationToken cancellationToken) => service.AuditAsync(request.ActorId, request.Id, request.Branch, request.Page, request.PageSize, cancellationToken);
}
public sealed class ReadAdminStoreValidator : AbstractValidator<ReadAdminStoreQuery>
{
    public ReadAdminStoreValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.Id).NotEmpty(); }
}
public sealed class ListAdminStoresValidator : AbstractValidator<ListAdminStoresQuery>
{
    public ListAdminStoresValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value).Must(value => MerchantReviewInput.Page(value.Page, value.PageSize)); }
}
public sealed class UpdateAdminStoreValidator : AbstractValidator<UpdateAdminStoreCommand>
{
    public UpdateAdminStoreValidator()
    {
        RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.Id).NotEmpty();
        RuleFor(value => value.Value).Must(value => value is not null && MerchantReviewInput.Version(value.RowVersion) && Store.AdminReason(value.Reason) && StoreInput.Details(value.Details));
    }
}
public sealed class ModerateStoreValidator : AbstractValidator<ModerateStoreCommand>
{
    public ModerateStoreValidator()
    {
        RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.Id).NotEmpty(); RuleFor(value => value.Action).IsInEnum();
        RuleFor(value => value.Value).Must(value => value is not null && MerchantReviewInput.Version(value.RowVersion) && Store.AdminReason(value.Reason));
    }
}
public sealed class ChangeAdminStoreLogoValidator : AbstractValidator<ChangeAdminStoreLogoCommand>
{
    public ChangeAdminStoreLogoValidator()
    {
        RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.Id).NotEmpty(); RuleFor(value => value.RowVersion).Must(MerchantReviewInput.Version);
        RuleFor(value => value.Content).Must(value => value is null || value.CanRead); RuleFor(value => value.Reason).Must(Store.AdminReason);
    }
}
public sealed class ReadAdminStoreLogoValidator : AbstractValidator<ReadAdminStoreLogoQuery>
{
    public ReadAdminStoreLogoValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.Id).NotEmpty(); }
}
public sealed class ListAdminBranchesValidator : AbstractValidator<ListAdminBranchesQuery>
{
    public ListAdminBranchesValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.StoreId).NotEmpty(); RuleFor(value => value).Must(value => MerchantReviewInput.Page(value.Page, value.PageSize)); }
}
public sealed class UpdateAdminBranchValidator : AbstractValidator<UpdateAdminBranchCommand>
{
    public UpdateAdminBranchValidator()
    {
        RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.StoreId).NotEmpty(); RuleFor(value => value.Id).NotEmpty();
        RuleFor(value => value.Value).Must(value => value is not null && MerchantReviewInput.Version(value.RowVersion) && Store.AdminReason(value.Reason) && BranchInput.Details(value.Details));
    }
}
public sealed class ReadStoreAuditValidator : AbstractValidator<ReadStoreAuditQuery>
{
    public ReadStoreAuditValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.Id).NotEmpty(); RuleFor(value => value).Must(value => MerchantReviewInput.Page(value.Page, value.PageSize)); }
}
