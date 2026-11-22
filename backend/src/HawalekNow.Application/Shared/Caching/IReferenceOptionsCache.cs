using System.Collections.Immutable;
using HawalekNow.Application.Shared.Results;

namespace HawalekNow.Application.Shared.Caching;

public enum ReferenceDataFamily { Catalog, Locations }
public sealed record ReferenceOptionsKey(ReferenceDataFamily Family, string Resource, string Language, Guid? OwnerId, Guid? ParentId, int Page, int PageSize, bool ActiveOnly = true);
public sealed record ReferenceOptionSnapshot(Guid Id, string Name);
public sealed record ReferencePageSnapshot(ImmutableArray<ReferenceOptionSnapshot> Items, int Page, int PageSize, bool HasMore);
public interface IReferenceOptionsCache
{
    Task<Result<ReferencePageSnapshot>> GetAsync(ReferenceOptionsKey key, Func<CancellationToken, Task<Result<ReferencePageSnapshot>>> load, CancellationToken cancellationToken);
    void Invalidate(ReferenceDataFamily family);
}
