using System.Collections.Immutable;
using HawalekNow.Application.Modules.Locations;
using HawalekNow.Application.Shared.Caching;
using HawalekNow.Application.Shared.Results;

namespace HawalekNow.Infrastructure.Modules.Locations;

public sealed class CachedLocationQueries(LocationQueries queries, IReferenceOptionsCache cache) : ILocationQueries
{
    public async Task<Result<LocationPage<LocationOption>>> OptionsAsync(LocationKind kind, Guid? governorateId, Guid? cityId, string language, int page, int pageSize, CancellationToken cancellationToken)
    {
        var key = new ReferenceOptionsKey(ReferenceDataFamily.Locations, kind.ToString(), language, governorateId, cityId, page, pageSize);
        var result = await cache.GetAsync(key, async ct =>
        {
            var loaded = await queries.OptionsAsync(kind, governorateId, cityId, language, page, pageSize, ct);
            return loaded.IsSuccess ? Result<ReferencePageSnapshot>.Success(new(loaded.Value.Items.Select(item => new ReferenceOptionSnapshot(item.Id, item.Name)).ToImmutableArray(), loaded.Value.Page, loaded.Value.PageSize, loaded.Value.HasMore)) : Result<ReferencePageSnapshot>.Failure(loaded.Error!);
        }, cancellationToken);
        return result.IsSuccess ? Result<LocationPage<LocationOption>>.Success(new(result.Value.Items.Select(item => new LocationOption(item.Id, item.Name)).ToImmutableArray(), result.Value.Page, result.Value.PageSize, result.Value.HasMore)) : Result<LocationPage<LocationOption>>.Failure(result.Error!);
    }
    public Task<Result<LocationPage<LocationAdminDto>>> AdminAsync(LocationKind kind, Guid? governorateId, Guid? cityId, int page, int pageSize, CancellationToken cancellationToken) => queries.AdminAsync(kind, governorateId, cityId, page, pageSize, cancellationToken);
}
