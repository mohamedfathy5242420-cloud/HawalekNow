using HawalekNow.Application.Shared.Caching;
using HawalekNow.Application.Shared.Results;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HawalekNow.Infrastructure.Shared.Caching;

public sealed class ReferenceOptionsCache(TimeProvider clock) : IReferenceOptionsCache, IDisposable
{
    public const int SizeLimit = 8192;
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);
    private readonly object gate = new();
    private readonly long[] generations = new long[2];
    // A dedicated cache avoids applying SizeLimit to framework/identity entries.
    private readonly IMemoryCache memory = new MemoryCache(new MemoryCacheOptions { SizeLimit = SizeLimit, ExpirationScanFrequency = TimeSpan.FromSeconds(30) });
    private sealed record Entry(ReferencePageSnapshot Snapshot, DateTimeOffset ExpiresAt);
    public async Task<Result<ReferencePageSnapshot>> GetAsync(ReferenceOptionsKey key, Func<CancellationToken, Task<Result<ReferencePageSnapshot>>> load, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(key.Family)) throw new ArgumentOutOfRangeException(nameof(key));
        cancellationToken.ThrowIfCancellationRequested();
        long generation;
        lock (gate)
        {
            generation = generations[(int)key.Family];
            if (memory.TryGetValue((key, generation), out Entry? entry) && entry is not null && entry.ExpiresAt > clock.GetUtcNow()) return Result<ReferencePageSnapshot>.Success(entry.Snapshot);
        }
        var loaded = await load(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!loaded.IsSuccess) return loaded;
        if (loaded.Value.Items.IsDefault || loaded.Value.Items.Length > 100 || !key.ActiveOnly) throw new InvalidOperationException("Only bounded public reference options may be cached.");
        lock (gate)
        {
            // Publish and invalidation share the gate; a stale loader cannot refill a new generation.
            if (generation == generations[(int)key.Family]) memory.Set((key, generation), new Entry(loaded.Value, clock.GetUtcNow() + Lifetime), new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime, Size = 1 + loaded.Value.Items.Length });
        }
        return loaded;
    }
    public void Invalidate(ReferenceDataFamily family)
    {
        if (!Enum.IsDefined(family)) throw new ArgumentOutOfRangeException(nameof(family));
        lock (gate) generations[(int)family]++;
    }
    public void Dispose() => memory.Dispose();
}
public static class ReferenceCacheRegistration
{
    public static IServiceCollection AddReferenceOptionsCache(this IServiceCollection services)
    { services.TryAddSingleton(TimeProvider.System); services.TryAddSingleton<IReferenceOptionsCache, ReferenceOptionsCache>(); return services; }
}
