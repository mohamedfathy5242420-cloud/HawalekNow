using System.Collections.Immutable;
using HawalekNow.Application.Shared.Caching;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Infrastructure.Shared.Caching;

namespace HawalekNow.UnitTests;

public sealed class ReferenceOptionsCacheTests
{
    private static ReferenceOptionsKey Key => new(ReferenceDataFamily.Catalog, "categories", "ar", Guid.NewGuid(), null, 1, 30);
    private static Result<ReferencePageSnapshot> Page(string name) => Result<ReferencePageSnapshot>.Success(new([new(Guid.NewGuid(), name)], 1, 30, false));
    [Fact]
    public async Task Hit_is_immutable_and_absolute_ttl_expires_even_when_read()
    {
        var clock = new Clock(); using var cache = new ReferenceOptionsCache(clock); var key = Key; var count = 0;
        Task<Result<ReferencePageSnapshot>> Load(CancellationToken _) { count++; return Task.FromResult(Page("أصل")); }
        var first = await cache.GetAsync(key, Load, default); clock.Now += TimeSpan.FromSeconds(59); var hit = await cache.GetAsync(key, Load, default); Assert.Same(first.Value, hit.Value);
        var changedCopy = hit.Value.Items.SetItem(0, hit.Value.Items[0] with { Name = "نسخة" }); Assert.Equal("أصل", first.Value.Items[0].Name); Assert.Equal("نسخة", changedCopy[0].Name);
        clock.Now += TimeSpan.FromSeconds(2); await cache.GetAsync(key, Load, default); Assert.Equal(2, count);
    }
    [Fact]
    public async Task Keys_separate_language_parents_filters_pages_resources_and_family()
    {
        using var cache = new ReferenceOptionsCache(TimeProvider.System); var key = Key; var count = 0;
        Task<Result<ReferencePageSnapshot>> Load(CancellationToken _) { count++; return Task.FromResult(Page(count.ToString())); }
        var variants = new[] { key, key with { Language = "en" }, key with { OwnerId = Guid.NewGuid() }, key with { ParentId = Guid.NewGuid() }, key with { Page = 2 }, key with { PageSize = 10 }, key with { Resource = "activities" }, key with { Family = ReferenceDataFamily.Locations } };
        foreach (var item in variants) { await cache.GetAsync(item, Load, default); await cache.GetAsync(item, Load, default); }
        Assert.Equal(variants.Length, count);
        cache.Invalidate(ReferenceDataFamily.Catalog); await cache.GetAsync(variants[^1], Load, default); Assert.Equal(variants.Length, count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync(key with { ActiveOnly = false }, Load, default));
    }
    [Fact]
    public async Task Old_loader_can_finish_but_cannot_republish_after_invalidation()
    {
        using var cache = new ReferenceOptionsCache(TimeProvider.System); var key = Key; var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = cache.GetAsync(key, async _ => { entered.SetResult(); await release.Task; return Page("old"); }, default); await entered.Task;
        cache.Invalidate(key.Family); var fresh = await cache.GetAsync(key, _ => Task.FromResult(Page("new")), default); release.SetResult(); Assert.Equal("old", (await old).Value.Items[0].Name);
        var after = await cache.GetAsync(key, _ => throw new InvalidOperationException("A successful fresh value was replaced."), default); Assert.Same(fresh.Value, after.Value);
    }
    [Fact]
    public async Task Failure_or_cancellation_is_never_cached_and_oversized_values_are_rejected()
    {
        using var cache = new ReferenceOptionsCache(TimeProvider.System); var key = Key; var count = 0;
        async Task<Result<ReferencePageSnapshot>> Fail(CancellationToken _) { count++; await Task.Yield(); return Result<ReferencePageSnapshot>.Failure(new(ErrorKind.Validation)); }
        await cache.GetAsync(key, Fail, default); await cache.GetAsync(key, Fail, default); Assert.Equal(2, count);
        using var cancellation = new CancellationTokenSource(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetAsync(key, _ => { cancellation.Cancel(); return Task.FromResult(Page("canceled")); }, cancellation.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync(key, _ => Task.FromResult(Result<ReferencePageSnapshot>.Success(new(Enumerable.Range(0, 101).Select(index => new ReferenceOptionSnapshot(Guid.NewGuid(), index.ToString())).ToImmutableArray(), 1, 101, false))), default));
        Assert.Equal("valid", (await cache.GetAsync(key, _ => Task.FromResult(Page("valid")), default)).Value.Items[0].Name);
    }
    [Fact]
    public async Task Size_limit_stops_unbounded_retention_without_blocking_sql_fallback()
    {
        using var cache = new ReferenceOptionsCache(TimeProvider.System); var calls = 0; var key = Key;
        Task<Result<ReferencePageSnapshot>> Load(CancellationToken _) { calls++; return Task.FromResult(Page("bounded")); }
        // Every entry costs2 units. This exceeds the8192-unit limit even if compaction runs.
        var keys = Enumerable.Range(1, ReferenceOptionsCache.SizeLimit).Select(page => key with { Page = page }).ToArray();
        foreach (var item in keys) await cache.GetAsync(item, Load, default);
        foreach (var item in keys) await cache.GetAsync(item, Load, default);
        Assert.True(calls > keys.Length); Assert.True(calls <= keys.Length * 2);
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
}
