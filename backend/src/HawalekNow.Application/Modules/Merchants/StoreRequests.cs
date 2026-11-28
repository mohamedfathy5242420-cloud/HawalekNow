using FluentValidation;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Merchants;
using MediatR;

namespace HawalekNow.Application.Modules.Merchants;

public sealed record StoreWrite(string Name, string? Phone = null, string? WhatsApp = null, string? Facebook = null, string? Instagram = null, string? TikTok = null);
public sealed record StoreCreateWrite(Guid ActivityId, StoreWrite Details);
public sealed record StoreUpdateWrite(string RowVersion, StoreWrite Details);
public sealed record StoreDto(Guid Id, Guid MerchantAccountId, Guid ActivityId, string Name, string? Phone, string? WhatsApp,
    string? Facebook, string? Instagram, string? TikTok, bool HasLogo, string RowVersion, bool IsHidden, bool IsSuspended, string? VisibilityReason, string? SuspensionReason, bool CanOwnerEdit);
public sealed record PublicStoreDto(Guid Id, Guid ActivityId, string Name, string? Phone, string? WhatsApp,
    string? Facebook, string? Instagram, string? TikTok, bool HasLogo);

public interface IStoreService
{
    Task<Result<StoreDto>> CreateAsync(Guid actorId, StoreCreateWrite value, CancellationToken cancellationToken);
    Task<Result<StoreDto>> UpdateAsync(Guid actorId, Guid id, StoreUpdateWrite value, CancellationToken cancellationToken);
    Task<Result<StoreDto>> OwnAsync(Guid actorId, CancellationToken cancellationToken);
    Task<Result<PublicStoreDto>> PublicAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<StoreDto>> ChangeLogoAsync(Guid actorId, Guid id, string rowVersion, Stream? content, CancellationToken cancellationToken);
    Task<Result<byte[]>> ReadLogoAsync(Guid? actorId, Guid id, CancellationToken cancellationToken);
}
public sealed record CreateStoreCommand(Guid ActorId, StoreCreateWrite Value) : IRequest<Result<StoreDto>>;
public sealed record UpdateStoreCommand(Guid ActorId, Guid Id, StoreUpdateWrite Value) : IRequest<Result<StoreDto>>;
public sealed record GetOwnStoreQuery(Guid ActorId) : IRequest<Result<StoreDto>>;
public sealed record GetPublicStoreQuery(Guid Id) : IRequest<Result<PublicStoreDto>>;
public sealed record ChangeStoreLogoCommand(Guid ActorId, Guid Id, string RowVersion, Stream? Content) : IRequest<Result<StoreDto>>;
public sealed record GetStoreLogoQuery(Guid? ActorId, Guid Id) : IRequest<Result<byte[]>>;

public sealed class CreateStoreHandler(IStoreService service) : IRequestHandler<CreateStoreCommand, Result<StoreDto>>
{
    public Task<Result<StoreDto>> Handle(CreateStoreCommand request, CancellationToken cancellationToken) => service.CreateAsync(request.ActorId, request.Value, cancellationToken);
}
public sealed class UpdateStoreHandler(IStoreService service) : IRequestHandler<UpdateStoreCommand, Result<StoreDto>>
{
    public Task<Result<StoreDto>> Handle(UpdateStoreCommand request, CancellationToken cancellationToken) => service.UpdateAsync(request.ActorId, request.Id, request.Value, cancellationToken);
}
public sealed class GetOwnStoreHandler(IStoreService service) : IRequestHandler<GetOwnStoreQuery, Result<StoreDto>>
{
    public Task<Result<StoreDto>> Handle(GetOwnStoreQuery request, CancellationToken cancellationToken) => service.OwnAsync(request.ActorId, cancellationToken);
}
public sealed class GetPublicStoreHandler(IStoreService service) : IRequestHandler<GetPublicStoreQuery, Result<PublicStoreDto>>
{
    public Task<Result<PublicStoreDto>> Handle(GetPublicStoreQuery request, CancellationToken cancellationToken) => service.PublicAsync(request.Id, cancellationToken);
}
public sealed class ChangeStoreLogoHandler(IStoreService service) : IRequestHandler<ChangeStoreLogoCommand, Result<StoreDto>>
{
    public Task<Result<StoreDto>> Handle(ChangeStoreLogoCommand request, CancellationToken cancellationToken) => service.ChangeLogoAsync(request.ActorId, request.Id, request.RowVersion, request.Content, cancellationToken);
}
public sealed class GetStoreLogoHandler(IStoreService service) : IRequestHandler<GetStoreLogoQuery, Result<byte[]>>
{
    public Task<Result<byte[]>> Handle(GetStoreLogoQuery request, CancellationToken cancellationToken) => service.ReadLogoAsync(request.ActorId, request.Id, cancellationToken);
}

public static class StoreInput
{
    public static bool Details(StoreWrite? value) => Normalize(value, out _);
    public static bool Normalize(StoreWrite? value, out StoreDetails? details)
    {
        details = null;
        return value is not null && StoreDetails.TryCreate(value.Name, value.Phone, value.WhatsApp, value.Facebook, value.Instagram, value.TikTok, out details);
    }
}
public sealed class CreateStoreValidator : AbstractValidator<CreateStoreCommand>
{
    public CreateStoreValidator()
    {
        RuleFor(value => value.ActorId).NotEmpty();
        RuleFor(value => value.Value).Must(value => value is not null && value.ActivityId != Guid.Empty && StoreInput.Details(value.Details));
    }
}
public sealed class UpdateStoreValidator : AbstractValidator<UpdateStoreCommand>
{
    public UpdateStoreValidator()
    {
        RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.Id).NotEmpty();
        RuleFor(value => value.Value).Must(value => value is not null && MerchantReviewInput.Version(value.RowVersion) && StoreInput.Details(value.Details));
    }
}
public sealed class GetOwnStoreValidator : AbstractValidator<GetOwnStoreQuery>
{
    public GetOwnStoreValidator() => RuleFor(value => value.ActorId).NotEmpty();
}
public sealed class GetPublicStoreValidator : AbstractValidator<GetPublicStoreQuery>
{
    public GetPublicStoreValidator() => RuleFor(value => value.Id).NotEmpty();
}
public sealed class ChangeStoreLogoValidator : AbstractValidator<ChangeStoreLogoCommand>
{
    public ChangeStoreLogoValidator()
    {
        RuleFor(value => value.ActorId).NotEmpty(); RuleFor(value => value.Id).NotEmpty();
        RuleFor(value => value.RowVersion).Must(MerchantReviewInput.Version); RuleFor(value => value.Content).Must(value => value is null || value.CanRead);
    }
}
public sealed class GetStoreLogoValidator : AbstractValidator<GetStoreLogoQuery>
{
    public GetStoreLogoValidator() { RuleFor(value => value.Id).NotEmpty(); RuleFor(value => value.ActorId).Must(value => value is null || value != Guid.Empty); }
}
