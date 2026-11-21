using System.Collections.Immutable;

namespace HawalekNow.Application.Modules.Catalog.Contracts;

public sealed record InitialActivitiesOutcome(int Added, ImmutableDictionary<string, Guid> Keys);
public interface IInitialActivities
{
    // The caller owns one shared UoW transaction. This operation never commits it.
    Task<InitialActivitiesOutcome> SeedInsideTransactionAsync(CancellationToken cancellationToken);
}
