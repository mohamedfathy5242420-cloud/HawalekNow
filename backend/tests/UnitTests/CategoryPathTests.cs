using HawalekNow.Domain.Modules.Catalog;

namespace HawalekNow.UnitTests;

public sealed class CategoryPathTests
{
    [Fact]
    public void Direct_and_indirect_cycles_and_wrong_activities_are_rejected()
    {
        var id = Guid.CreateVersion7(); var activity = Guid.CreateVersion7(); var ancestor = Guid.CreateVersion7();
        var path = new CategoryPath(id, activity);
        Assert.False(path.TryAppend(id, activity));
        Assert.False(path.TryAppend(ancestor, Guid.CreateVersion7()));
        Assert.True(path.TryAppend(ancestor, activity));
        Assert.False(path.TryAppend(ancestor, activity));
        Assert.False(path.TryAppend(Guid.Empty, activity));
    }
    [Fact]
    public void Deep_valid_paths_have_no_fixed_business_limit()
    {
        var activity = Guid.CreateVersion7(); var path = new CategoryPath(Guid.CreateVersion7(), activity);
        for (var i = 0; i < 20_000; i++) Assert.True(path.TryAppend(Guid.CreateVersion7(), activity));
    }
}
