using HawalekNow.Application.Shared.Results;
using MediatR;

namespace HawalekNow.Application.Modules.Identity;

public sealed record CustomerIdentityInfo(Guid UserId, string Username, string Email);
public sealed record AccountSessionInfo(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset LastRefreshedAt, DateTimeOffset ExpiresAt,
    DateTimeOffset AbsoluteExpiresAt, DateTimeOffset? RevokedAt, bool IsCurrent);
public interface IIdentityAccountQueries
{
    Task<CustomerIdentityInfo?> CustomerIdentityAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccountSessionInfo>> SessionsAsync(Guid userId, Guid currentSessionId, CancellationToken cancellationToken);
    Task<AccountSessionInfo?> SessionAsync(Guid userId, Guid sessionId, Guid currentSessionId, CancellationToken cancellationToken);
}
public sealed record MyCustomerIdentityQuery(Guid UserId) : IRequest<Result<CustomerIdentityInfo>>;
public sealed record MySessionsQuery(Guid UserId, Guid CurrentSessionId) : IRequest<Result<IReadOnlyList<AccountSessionInfo>>>;
public sealed record MySessionQuery(Guid UserId, Guid SessionId, Guid CurrentSessionId) : IRequest<Result<AccountSessionInfo>>;
public sealed class MyCustomerIdentityHandler(IIdentityAccountQueries queries) : IRequestHandler<MyCustomerIdentityQuery, Result<CustomerIdentityInfo>>
{
    public async Task<Result<CustomerIdentityInfo>> Handle(MyCustomerIdentityQuery request, CancellationToken cancellationToken) =>
        await queries.CustomerIdentityAsync(request.UserId, cancellationToken) is { } value ? Result<CustomerIdentityInfo>.Success(value) : Result<CustomerIdentityInfo>.Failure(new(ErrorKind.NotFound));
}
public sealed class MySessionsHandler(IIdentityAccountQueries queries) : IRequestHandler<MySessionsQuery, Result<IReadOnlyList<AccountSessionInfo>>>
{
    public async Task<Result<IReadOnlyList<AccountSessionInfo>>> Handle(MySessionsQuery request, CancellationToken cancellationToken) =>
        Result<IReadOnlyList<AccountSessionInfo>>.Success(await queries.SessionsAsync(request.UserId, request.CurrentSessionId, cancellationToken));
}
public sealed class MySessionHandler(IIdentityAccountQueries queries) : IRequestHandler<MySessionQuery, Result<AccountSessionInfo>>
{
    public async Task<Result<AccountSessionInfo>> Handle(MySessionQuery request, CancellationToken cancellationToken) =>
        await queries.SessionAsync(request.UserId, request.SessionId, request.CurrentSessionId, cancellationToken) is { } value ? Result<AccountSessionInfo>.Success(value) : Result<AccountSessionInfo>.Failure(new(ErrorKind.NotFound));
}
