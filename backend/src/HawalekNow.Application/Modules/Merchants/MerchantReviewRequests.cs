using System.Collections.Immutable;
using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Merchants;
using MediatR;

namespace HawalekNow.Application.Modules.Merchants;

public enum MerchantReviewAction { Approve = 1, RequestChanges = 2, Reject = 3, Suspend = 4, Reinstate = 5 }
public sealed record MerchantReviewWrite(string RowVersion, string? Reason = null);
public sealed record MerchantResubmitWrite(string RowVersion);
public sealed record MerchantReviewDto(Guid Id, Guid UserId, string? UserName, string? Email, bool EmailConfirmed, string Status,
    string? ReviewReason, DateTimeOffset? ReviewedAt, string RowVersion);
public sealed record MerchantAuditDto(Guid Id, Guid ActorId, string Operation, Guid TargetId, string? Reason,
    DateTimeOffset OccurredAt, string? PreviousState, string? CurrentState);
public sealed record MerchantPage<T>(ImmutableArray<T> Items, int Page, int PageSize, bool HasMore);

public interface IMerchantReviewService
{
    Task<Result<MerchantReviewDto>> ReviewAsync(Guid actorId, Guid id, MerchantReviewAction action, MerchantReviewWrite value, CancellationToken cancellationToken);
    Task<Result<MerchantReviewDto>> ResubmitAsync(Guid actorId, string rowVersion, CancellationToken cancellationToken);
    Task<Result<MerchantReviewDto>> OwnAsync(Guid actorId, CancellationToken cancellationToken);
    Task<Result<MerchantPage<MerchantReviewDto>>> ListAsync(Guid actorId, string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<MerchantPage<MerchantAuditDto>>> AuditAsync(Guid actorId, Guid id, int page, int pageSize, CancellationToken cancellationToken);
}
public sealed record ReviewMerchantCommand(Guid ActorId, Guid Id, MerchantReviewAction Action, MerchantReviewWrite Value) : IRequest<Result<MerchantReviewDto>>;
public sealed record ResubmitMerchantCommand(Guid ActorId, string RowVersion) : IRequest<Result<MerchantReviewDto>>;
public sealed record GetOwnMerchantReviewQuery(Guid ActorId) : IRequest<Result<MerchantReviewDto>>;
public sealed record ListMerchantReviewsQuery(Guid ActorId, string? Status = null, int Page = 1, int PageSize = 25) : IRequest<Result<MerchantPage<MerchantReviewDto>>>;
public sealed record ListMerchantAuditQuery(Guid ActorId, Guid Id, int Page = 1, int PageSize = 25) : IRequest<Result<MerchantPage<MerchantAuditDto>>>;
public sealed class ReviewMerchantHandler(IMerchantReviewService service) : IRequestHandler<ReviewMerchantCommand, Result<MerchantReviewDto>>
{
    public Task<Result<MerchantReviewDto>> Handle(ReviewMerchantCommand request, CancellationToken cancellationToken) =>
        service.ReviewAsync(request.ActorId, request.Id, request.Action, request.Value, cancellationToken);
}
public sealed class ResubmitMerchantHandler(IMerchantReviewService service) : IRequestHandler<ResubmitMerchantCommand, Result<MerchantReviewDto>>
{
    public Task<Result<MerchantReviewDto>> Handle(ResubmitMerchantCommand request, CancellationToken cancellationToken) => service.ResubmitAsync(request.ActorId, request.RowVersion, cancellationToken);
}
public sealed class GetOwnMerchantReviewHandler(IMerchantReviewService service) : IRequestHandler<GetOwnMerchantReviewQuery, Result<MerchantReviewDto>>
{
    public Task<Result<MerchantReviewDto>> Handle(GetOwnMerchantReviewQuery request, CancellationToken cancellationToken) => service.OwnAsync(request.ActorId, cancellationToken);
}
public sealed class ListMerchantReviewsHandler(IMerchantReviewService service) : IRequestHandler<ListMerchantReviewsQuery, Result<MerchantPage<MerchantReviewDto>>>
{
    public Task<Result<MerchantPage<MerchantReviewDto>>> Handle(ListMerchantReviewsQuery request, CancellationToken cancellationToken) => service.ListAsync(request.ActorId, request.Status, request.Page, request.PageSize, cancellationToken);
}
public sealed class ListMerchantAuditHandler(IMerchantReviewService service) : IRequestHandler<ListMerchantAuditQuery, Result<MerchantPage<MerchantAuditDto>>>
{
    public Task<Result<MerchantPage<MerchantAuditDto>>> Handle(ListMerchantAuditQuery request, CancellationToken cancellationToken) => service.AuditAsync(request.ActorId, request.Id, request.Page, request.PageSize, cancellationToken);
}
public static class MerchantReviewInput
{
    public static bool Version(string? value) => value is not null && Convert.TryFromBase64String(value, new byte[8], out var length) && length == 8;
    public static bool Write(MerchantReviewWrite? value, MerchantReviewAction action) => value is not null && Enum.IsDefined(action) && Version(value.RowVersion) &&
        (value.Reason is null || value.Reason.Length <= 1000 && !value.Reason.Contains('\0')) && (action == MerchantReviewAction.Approve || !string.IsNullOrWhiteSpace(value.Reason));
    public static bool Page(int page, int size) => page is >= 1 and <= 100000 && size is >= 1 and <= 100;
    public static bool Status(string? value) => value is null || Enum.GetNames<MerchantApprovalStatus>().Any(name => name.Equals(value, StringComparison.OrdinalIgnoreCase));
}
public sealed class ReviewMerchantValidator : AbstractValidator<ReviewMerchantCommand>
{
    public ReviewMerchantValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.Id).NotEmpty(); RuleFor(value => value).Must(value => MerchantReviewInput.Write(value.Value, value.Action)); }
}
public sealed class ResubmitMerchantValidator : AbstractValidator<ResubmitMerchantCommand>
{
    public ResubmitMerchantValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.RowVersion).Must(MerchantReviewInput.Version); }
}
public sealed class GetOwnMerchantReviewValidator : AbstractValidator<GetOwnMerchantReviewQuery>
{
    public GetOwnMerchantReviewValidator() => RuleFor(value => value.ActorId).NotEmpty();
}
public sealed class ListMerchantReviewsValidator : AbstractValidator<ListMerchantReviewsQuery>
{
    public ListMerchantReviewsValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value).Must(value => MerchantReviewInput.Page(value.Page, value.PageSize) && MerchantReviewInput.Status(value.Status)); }
}
public sealed class ListMerchantAuditValidator : AbstractValidator<ListMerchantAuditQuery>
{
    public ListMerchantAuditValidator() { RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.Id).NotEmpty(); RuleFor(value => value).Must(value => MerchantReviewInput.Page(value.Page, value.PageSize)); }
}
