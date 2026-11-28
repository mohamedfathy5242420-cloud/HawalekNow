using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Merchants;
using MediatR;

namespace HawalekNow.Application.Modules.Merchants;

public sealed record ActivityChangeWrite(Guid ActivityId, string StoreRowVersion, string? Reason = null);
public sealed record ActivityChangeReviewWrite(string RowVersion, string StoreRowVersion, string? Reason = null);
public sealed record ActivityChangeDto(Guid Id, Guid StoreId, Guid PreviousActivityId, Guid RequestedActivityId, Guid CurrentActivityId, string Status,
    Guid RequestedBy, DateTimeOffset RequestedAt, string? RequestReason, Guid? ReviewedBy, DateTimeOffset? ReviewedAt, string? ReviewReason, string RowVersion, string StoreRowVersion);

public interface IActivityChangeService
{
    Task<Result<ActivityChangeDto>> RequestAsync(Guid actorId, Guid storeId, ActivityChangeWrite value, CancellationToken cancellationToken);
    Task<Result<ActivityChangeDto>> ReviewAsync(Guid actorId, Guid id, bool approve, ActivityChangeReviewWrite value, CancellationToken cancellationToken);
    Task<Result<MerchantPage<ActivityChangeDto>>> OwnListAsync(Guid actorId, Guid storeId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<MerchantPage<ActivityChangeDto>>> AdminListAsync(Guid actorId, string? status, int page, int pageSize, CancellationToken cancellationToken);
}
public sealed record RequestActivityChangeCommand(Guid ActorId, Guid StoreId, ActivityChangeWrite Value) : IRequest<Result<ActivityChangeDto>>;
public sealed record ReviewActivityChangeCommand(Guid ActorId, Guid Id, bool Approve, ActivityChangeReviewWrite Value) : IRequest<Result<ActivityChangeDto>>;
public sealed record OwnActivityChangesQuery(Guid ActorId, Guid StoreId, int Page = 1, int PageSize = 25) : IRequest<Result<MerchantPage<ActivityChangeDto>>>;
public sealed record AdminActivityChangesQuery(Guid ActorId, string? Status = null, int Page = 1, int PageSize = 25) : IRequest<Result<MerchantPage<ActivityChangeDto>>>;
public sealed class RequestActivityChangeHandler(IActivityChangeService service) : IRequestHandler<RequestActivityChangeCommand, Result<ActivityChangeDto>>
{
    public Task<Result<ActivityChangeDto>> Handle(RequestActivityChangeCommand request, CancellationToken cancellationToken) => service.RequestAsync(request.ActorId, request.StoreId, request.Value, cancellationToken);
}
public sealed class ReviewActivityChangeHandler(IActivityChangeService service) : IRequestHandler<ReviewActivityChangeCommand, Result<ActivityChangeDto>>
{
    public Task<Result<ActivityChangeDto>> Handle(ReviewActivityChangeCommand request, CancellationToken cancellationToken) => service.ReviewAsync(request.ActorId, request.Id, request.Approve, request.Value, cancellationToken);
}
public sealed class OwnActivityChangesHandler(IActivityChangeService service) : IRequestHandler<OwnActivityChangesQuery, Result<MerchantPage<ActivityChangeDto>>>
{
    public Task<Result<MerchantPage<ActivityChangeDto>>> Handle(OwnActivityChangesQuery request, CancellationToken cancellationToken) => service.OwnListAsync(request.ActorId, request.StoreId, request.Page, request.PageSize, cancellationToken);
}
public sealed class AdminActivityChangesHandler(IActivityChangeService service) : IRequestHandler<AdminActivityChangesQuery, Result<MerchantPage<ActivityChangeDto>>>
{
    public Task<Result<MerchantPage<ActivityChangeDto>>> Handle(AdminActivityChangesQuery request, CancellationToken cancellationToken) => service.AdminListAsync(request.ActorId, request.Status, request.Page, request.PageSize, cancellationToken);
}
public static class ActivityChangeInput
{
    public static bool Write(ActivityChangeWrite? value) => value is not null && value.ActivityId != Guid.Empty && MerchantReviewInput.Version(value.StoreRowVersion) && StoreActivityChange.ValidReason(value.Reason);
    public static bool Review(ActivityChangeReviewWrite? value, bool approve) => value is not null && MerchantReviewInput.Version(value.RowVersion) && MerchantReviewInput.Version(value.StoreRowVersion) && StoreActivityChange.ValidReason(value.Reason) && (approve || !string.IsNullOrWhiteSpace(value.Reason));
    public static bool Status(string? value) => value is null || Enum.GetNames<ActivityChangeStatus>().Any(name => name.Equals(value, StringComparison.OrdinalIgnoreCase));
}
public sealed class RequestActivityChangeValidator : AbstractValidator<RequestActivityChangeCommand>
{
    public RequestActivityChangeValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.StoreId).NotEmpty(); RuleFor(value => value.Value).Must(ActivityChangeInput.Write); }
}
public sealed class ReviewActivityChangeValidator : AbstractValidator<ReviewActivityChangeCommand>
{
    public ReviewActivityChangeValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.Id).NotEmpty(); RuleFor(value => value).Must(value => ActivityChangeInput.Review(value.Value, value.Approve)); }
}
public sealed class OwnActivityChangesValidator : AbstractValidator<OwnActivityChangesQuery>
{
    public OwnActivityChangesValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.StoreId).NotEmpty(); RuleFor(value => value).Must(value => MerchantReviewInput.Page(value.Page, value.PageSize)); }
}
public sealed class AdminActivityChangesValidator : AbstractValidator<AdminActivityChangesQuery>
{
    public AdminActivityChangesValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value).Must(value => ActivityChangeInput.Status(value.Status) && MerchantReviewInput.Page(value.Page, value.PageSize)); }
}
